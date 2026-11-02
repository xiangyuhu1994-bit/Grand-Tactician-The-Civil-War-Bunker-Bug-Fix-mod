## AI weapon purchases and the career commander-in-chief (src/AiWeapons.cs)

### 1. AI weapon value overrates accuracy
**Bug.** `AICampaign.GetWeaponStrengthFactor` (AICampaign.cs ~7543) rates a weapon as `range × ROF^0.2 × firepower/2 × accuracy³` (guns also divided by tube weight^0.2). Accuracy cubed plus raw range makes the Whitworth P1857 3.8× a Springfield and the 3-inch Ordnance Rifle 3.2× a Napoleon, while repeaters barely beat a rifle-musket. Everything the AI does with weapons runs on this number: what it orders, what `GetBestWeapon` hands out when re-arming units and the side averages. The career DLC also uses it for prestige costs.

**Fix.** The rating uses the same shape as the auto-resolve weapon term: `√(firepower × (ROF/3)^0.7 × (accuracy/0.9)² × (range/350)^0.5)` for small arms (raw weapon accuracy, since a purchase has no crew yet). Guns use the mean of their positive firepower values, ROF capped at 6 (so Gatlings and the Williams gun don't run away with it), raw accuracy² and `(range/1600)^0.5`. The exponents are the auto-resolve config values, so changing `RofExponent` changes both. Results are scaled so the Springfield M1855/61 and the 12-pounder Napoleon keep their vanilla values.

Rifles, relative to the Springfield (vanilla → new): Enfield P1853 1.09 → 1.05, Whitworth P1857 3.87 → 1.50, Sharps rifle 2.68 → 1.76, Spencer rifle 1.00 → 1.49, Spencer carbine 1.03 → 1.54, Springfield M1866 3.02 → 1.85, M1842 musket 0.79 → 1.03, mixed smoothbores 0.59 → 0.93.
Guns, relative to the Napoleon: 6-pounder 0.55 → 0.78, 10-pdr Parrott 1.69 → 1.02, 3-inch Ordnance 3.20 → 1.38, 12-pdr Whitworth 2.23 → 1.25, 3-inch Armstrong 3.04 → 1.41, Gatling 4.56 → 1.38, Williams gun 1.38 → 0.74.

Career prestige keeps the vanilla rating, which prices premium weapons well: `GetWeaponUpgradePrestigeCost`, `GetRaiseUnitCost` and `GetCommandPrestigeCost` see vanilla values (`GetBestWeapon`, called inside the upgrade cost to choose the weapon, still uses the new rating), and the mod's captured-weapon prestige compares vanilla ratings against a vanilla-rated side average.

Toggle: `Campaign.AiWeaponValue`.

### 2. AI orders 27,000 of an expensive rifle at a time
**Bug.** `CheckPurchaseWeapons` (AICampaign.cs ~7588) runs for every AI side (and the player's side when weapons are auto-managed). If the stock plus pending orders of above-average weapons would arm fewer units than `aiminnumberofequippableunits`, it picks a *random* researched or imported weapon rated above the side's average and orders a fixed `unit size × aiweaponordersmultiplerifles` (3,000 × 9 = 27,000 rifles) or `battery × aiweaponordersmultipleguns` guns. It never looks at price or at how many men actually need the weapon. `PlaceWeaponOrder` charges the whole cost up front, so one Whitworth order is a huge, mostly wasted expense. Re-arming (`UpgradeWeaponForUnit`) only uses delivered stock, so units don't actually get weapons before they arrive – but deliveries trickle in from the first day, so the first brigades switch over almost at once.

**Fix.** The function is replaced, keeping vanilla's gate (player side only when weapons are auto-managed; credit rating must allow purchases). For rifles and guns separately:
- Candidates: researched or imported, orderable, no order already running, rated above the side's average.
- Need: men (or guns) in units the candidate could arm (unit-type and horse-artillery restrictions) whose current weapon it beats by at least 10 % – unarmed units always count – minus the stock and pending orders of weapons at least as good.
- Pick: the largest average rating gain per man per dollar.
- Quantity: the need, capped by vanilla's order size. Orders under a quarter-regiment or one battery are skipped. The treasury is deliberately not a cap: sides are meant to run deficits and borrow more as the war goes on.

Toggle: `Campaign.AiWeaponPurchases`.

### 3. Career: the player can never become commander-in-chief
**Bug.** `UpdateCommanderInChief` (BattleUnits.cs ~3581) re-picks the CiC every `updatetimecommanderinchief` (0.25 years) unless the Lt. General policy freezes it, choosing the highest `campaignranking = command level × 100000 + rank × 10000 + exp × 1000 + fame × 100`. `GetCommandLevel` scores the sitting CiC as 18 (army group) wherever he serves, while an army commander scores 16. The incumbent is therefore always ahead by the level term alone. Separately, `DLC_WL.IsPromotable` won't promote the player above his immediate superior – and when he commands a top-level army that superior is the CiC – so he can't win on rank either. The only ways in were the incumbent dying, being captured or otherwise becoming unavailable.

**Fix.** At each re-pick, if the player is available, commands a top-level army (no parent formation) and is at least the CiC's rank, he is scored at level 18 like the incumbent for that comparison. Rank, then experience, then fame decide. The game's own "becoming commanding general" event, diary entry and prestige handling then fire as normal. The Lt. General policy freeze still applies.

Toggle: `Campaign.CareerCicPath`.

**Not changed:** the "CiC unavailable" flag in `UpdateCommanderInChief` is never reset between alliances, so one side's CiC being unavailable forces a re-pick for every later side in the list too. Harmless in practice (it just re-picks the same top-ranked commander).
