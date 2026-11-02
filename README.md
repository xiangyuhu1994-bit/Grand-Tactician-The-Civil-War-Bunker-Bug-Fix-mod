# bugFixMod – Grand Tactician: The Civil War

An unofficial BepInEx 5 plugin that fixes a set of campaign, battle and auto-resolve bugs found by reading the decompiled game code. It also adds optional artillery realism and a fuller battle report.

**Status: loads cleanly in game (all patches apply) and every Harmony target is verified against the game DLL, but not every fix has been exercised in play yet.** Back up your saves. Every fix can be switched off in the config file.

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (the `win_x64` 5.4.x zip) into the game folder – unzip so `winhttp.dll` sits next to the game's .exe – and run the game once.
2. Download `bugFixMod.zip` from the latest [Release](../../releases) and copy `BepInEx/plugins/bugFixMod.dll` from it into the game's `BepInEx/plugins/` folder.
3. Run the game once to create `BepInEx/config/aom.bugfixmod.cfg`. Every fix and feature has its own switch there. The artillery features are off by default; everything else is on.
4. Check `BepInEx/LogOutput.log`. The last bugFixMod line reads "loaded: N patches applied, M failed". Any failed patch is named in an error line, and only that fix is inactive; everything else still applies. The IL patches also log how many code sites each changed; a count of 0 means a game update moved that code and that fix is inactive.

## What's in it

| Area | Switch | Default |
|---|---|---|
| HQs with no troops on the spot need a supporting unit to fight; sieges with no fighting men on one side end at once | Campaign.EmptyHQRules | on |
| AI doesn't send HQs with no troops into enemy territory | Campaign.AiLoneHQ | on |
| Fleets can bombard a fort that is already under siege | Campaign.FleetBombardBesiegedFort | on |
| Fleet disintegration ping-pong: only safe harbors; repeated escapes remove the fleet | Campaign.FleetDisintegrationLoopFix (+ RepeatDays 7, ScuttleShips off) | on |
| Level-3 fort sieges/bombardments no longer freeze | Campaign.LevelThreeForts | on |
| Auto-resolve: swapped perks, entrenchment side, counter-battery stance, last-unit picks | Campaign.AutoresolveFixes | on |
| Auto-resolve: ROF, range and accuracy count; 6-pounders inflict casualties | Autoresolve.WeaponQuality (+ exponents; RofExponent 0.7) | on |
| AI weapon rating uses the auto-resolve formula instead of accuracy cubed (career prestige keeps vanilla) | Campaign.AiWeaponValue | on |
| AI weapon orders sized to need, best rating gain per price | Campaign.AiWeaponPurchases | on |
| Career: player commanding a top-level army can be picked as commander-in-chief | Campaign.CareerCicPath | on |
| Tariff markup applies only to foreign links, not domestic ones | Campaign.TariffPerConnection | on |
| Union weapon import prices follow Union relations, not Confederate | Campaign.ImportPriceRelationsPerSide | on |
| Deserters no longer counted as prisoners the enemy took (repeatedly) | Campaign.DesertersNotPrisoners | on |
| Per-state Deserters figure counts men when they desert, not every unit's tally after every battle | Campaign.DeserterStatistic | on |
| Expired-contract men and deserters go home; a share of deserters returns to the army | Campaign.DesertersGoHome (+ ReturnShare 0.35, ReturnRate 3) | on |
| Paroled and freed prisoners add to their side's recruits instead of removing them | Campaign.PardonAddsRecruits | on |
| New prisoners go to loyal camps with room before captured camps | Campaign.PowCampPriority | on |
| Parole button on an over-capacity camp releases only the excess | Campaign.ParoleExcessOnly | on |
| Raiding an enemy POW camp frees prisoners | Campaign.RaidFreesPows (+ RaidPowFreeShare 0.5) | on |
| Raid effects no longer snowball with raid duration | Campaign.RaidTimerFix | on |
| Multi-day battle freeze | Battle.EndOfDayFreeze | on |
| Battle saves losing detachments and links | Battle.SaveReferences | on |
| Artillery won't fire through nearby friendly blocks or land shells next to its own troops (friendly fire stays on) | Battle.ArtilleryLineOfFire | on |
| Attack moves wiping charge/engage behaviour | Battle.KeepCombatBehaviourOnAttackMove | on |
| Cavalry Charge-off/Evade-off buttons | Battle.CavalryButtonOff | on |
| Skirmisher jitter when the enemy is between them and their brigade | Battle.SkirmisherFallback | on |
| Routed detachments running through enemies | Battle.RoutedReattachAvoidsEnemy | on |
| Column steps back before moving | Battle.ColumnBackStep | on |
| HQs moved alone no longer crawl at artillery pace; empty HQs not 30 % slow on the campaign map | Battle.HqSpeedFix | on |
| Custom/historical battles: individually placed player regiments follow group orders | Battle.CustomBattleReattach | on |
| Move orders not stopped by hidden or routed enemies (player side) | Battle.OrderInterruptionVisibleOnly | on |
| Stuck "interrupted" state cleared by new orders (career battles) | Battle.OrderInterruptionStaleClear | on |
| Charge end losing waypoints | Battle.ChargeEndRestoresAllWaypoints | on |
| Routed units surrender after enough total time in melee (vanilla almost never fires) | Surrender.CumulativeMeleeSurrender | on |
| Routed units can be run down by faster, large-enough pursuers | Surrender.RunDownSurrender | on |
| Infantry keeps chasing a routed charge target within the charge radius | Surrender.InfantryPursuit | on |
| Captured guns crewed by infantry/cavalry get 10 % ammo | Artillery.CapturedGunAmmo | 0.1 |
| Ammunition belongs to guns | Artillery.PerGunAmmo | off |
| Guns destroyed by solid shot | Artillery.GunDestruction / GunDestructionChance | off / 0.05 |
| Battle report lists captured weapons and officer casualties | Reporting.ExtendedBattleReport | on |
| Career DLC prestige for captured quality weapons and officers | Reporting.DlcCapturePrestige (+ factors) | on |
| Campaign OOB built once per click instead of 6–9 times | Performance.CampaignOOB | on |
| Save/load menu list built once, flag sprites cached | Performance.SaveMenu | on |
| Log the game's own per-section frame timers for profiling | Performance.PerfLog (+ PerfLogSeconds 15) | off |

## Compatibility

- Grand Tactician: The Civil War, game version current as of November 2026, with or without the career DLC. Built against Unity 2021.3, BepInEx 5.4.23 and HarmonyX 2.9.
- Works alongside config/data mods such as AOM, which don't change game code.
- New saves and existing saves both work. A few figures already stored in a save (for example inflated deserter totals) aren't recalculated.
- If a game update moves code a fix depends on, that fix logs an error or a "0 sites" line in `LogOutput.log` and stays inactive; everything else still applies.

## Building from source

1. Install the .NET SDK (6 or later).
2. Copy the game's `Grand Tactician_Data/Managed/Assembly-CSharp.dll` into a `lib/` folder in this repo (it is git-ignored – never commit game files), or pass its folder: `dotnet build -c Release -p:GameManaged="<path to Managed>"`.
3. `dotnet build -c Release` – the plugin is `bin/Release/netstandard2.0/bugFixMod.dll`.

`tools/` has a headless harness that applies every patch to the real game assembly under Mono, without starting the game.

## Reporting bugs

Open an issue with the mod version, what happened, and `BepInEx/LogOutput.log`. A save that reproduces it helps a lot.

---

# Bugs and fixes in detail

Line numbers refer to an ILSpy decompile of the current `Assembly-CSharp.dll`; class and method names are stable across updates, line numbers are not.

## Campaign fixes (src/Campaign.cs)

### Empty HQs starting battles and sieges – `EmptyHQRules`
**Bug.** An HQ with no units under it can start a siege, as attacker or defender, and the opposing force loses readiness and time to a siege against nobody.
- `Regiment.UpdateGroupStrength` hardcodes `groupmorale = 1` for an HQ with no attached units (Regiment.cs ~11319). It also forces `groupstrengthactive` to 1 (~11646).
- So the empty HQ passes the minimum-morale check in `BattleUnits.EstablishCampaignBattle` (BattleUnits.cs ~2933).
- Siege-type battles – a garrison is involved, or both sides are entrenched (`campaignstance == 1`) – skip the strength-ratio checks entirely (~3079).
- The siege then runs its setup time (`setuptimesiege[level]`, days) while draining readiness (`readinessconsumptionsieges`). Only after setup does the force-balance check notice one side has nothing and end it (Autocalc.cs ~2230–2265).

**Fix.** A prefix on `EstablishCampaignBattle`:
- **Empty army HQ (unittyp 18):** may start or join a battle or siege only if a friendly unit with troops is in range. "In range" uses the tests vanilla's `CheckBattleParticipation` uses to pull units into a fight: bugle range for sieges, command range otherwise, morale above the participation minimum, and the terrain-line check. When supported, vanilla runs unchanged, so the army commander can oversee the fight.
- **Unsupported empty army HQ:**
  - If it is the initiator, nothing happens – no battle and no withdrawal.
  - If an enemy with troops runs into it, it withdraws (`SetRetreatToDepot`, as vanilla does for low-morale units), so it can't sit there unattackable.
- **Empty corps, division or brigade HQs:** always withdraw.
- "Empty" means `groupstrength <= 0`. The raw sum is used because `groupstrengthactive` is faked to 1.


**Update (1.0.2) – why phantom sieges still happened, and three more fixes.**
- **"Empty" now means no troops on the spot.** An HQ's `groupstrength` counts every subordinate wherever it is. An HQ that had marched ahead of its divisions therefore looked full to the first version of this fix, and could still open a siege on its own. "Empty" now counts only fighting units under the HQ that are within its bugle range (`LocalStrength`). Friendly support is judged the same way.
  - Corps, division and brigade HQs with no units left anywhere still withdraw.
  - An HQ whose units are merely elsewhere is treated like an army HQ: it may fight only with friendly troops in range.
- **Sieges with nobody left on one side end at once** (`PhantomSiege_Patch`, prefix on `Autocalc.UpdateSiegeCombat`).
  - **Cause:** vanilla returns early while siege works are being set up (`setuptimesiege[level]`, about a day) – before it checks whether either side can still fight. A siege whose only attackers have left keeps draining the defenders' readiness until setup ends.
  - **Fix:** each update, if one side has no fighting men (non-withdrawn, not retreating units of unittyp ≤ 13 with strength), the siege ends through the game's own `FinishLandBattle`, with the other side winning. This also clears phantom sieges already running in a save.
- **AI no longer marches lone HQs into enemy territory** (`AiLoneHQ_Patch`, `Campaign.AiLoneHQ`).
  - `AICampaign.MoveUnitTo` (~5770) is the AI's single move order, and it is used for HQs with no troops too.
  - If the AI sends an HQ with no troops on the spot to a point on the enemy side of the front (`frontline2.GetSideOnPosition`), it is sent to the nearest friendly force with troops instead. If there is no such force, it stays put.

### Fleets can't bombard a besieged fort – `FleetBombardBesiegedFort`
**Bug.** This is an ordering problem in `EstablishCampaignBattle`.
- A besieged garrison has `inbattle = true`.
- The "either side already in a battle?" branch (BattleUnits.cs ~2954) comes before the fleet-bombardment branch (~3001).
- That branch only lets fleets join fleet battles and land units join land battles, then returns.
- So a fleet meeting a besieged garrison never reaches the bombardment code.
- The reverse works: `Autocalc.StartFortBombardment` doesn't set `inbattle` on the garrison, so a land army can still start a siege on a fort under bombardment.

**Fix.** The prefix runs the vanilla bombardment start (same conditions: fort condition and bugle range) for a fleet versus a garrison that is already in battle.
- Nothing in the bombardment update (`UpdateFortNavyEngagement`) reads `inbattle`.
- Siege and bombardment together is already a state vanilla reaches (bombard first, then besiege).
- Small extra fix: vanilla leaves a dead `Autocalc` component attached when `StartFortBombardment` returns null, which blocks later bombardments. The patch destroys it.

### Level-3 forts – `LevelThreeForts`
**Bug.**
- `GameVars.weapongroupforts` (fort level → weapon group) is hardcoded to `{ 0, 1, 2 }` (GameVars.cs:6028).
- Most callers clamp the level, but `Autocalc.GetAveragePenetrationFort` (Autocalc.cs:582, fleet bombardment), `GetTotalStrength(useforvictorycondition)` (:628, sieges) and `AICampaign` (:6135) don't.
- For a level-3 fort these throw `IndexOutOfRangeException` inside `Autocalc.Update`, so the siege or bombardment aborts every frame and never progresses or ends.

**Fix.** A postfix on `GameVars.ImportWeapons` sets the map to `{ 0, 1, 2, 3 }`.
- `weapongroups.dat` already has group 3 ("Fort Armory Level 3"), so level-3 forts now get their own guns instead of crashing.
- Level-indexed prefs arrays such as `setuptimesiege` have 6 entries, so level 3 is safe there.

## Battle fixes (src/Battle.cs)

### Artillery firing into its own lines on hills – `ArtilleryLineOfFire`
**Bug.**
- Targeting has no terrain line of sight. The only own-unit test, `Regiment.CheckOwnUnitInWay` (Regiment.cs ~9613), is a flat angle check. It also lets artillery overshoot any friendly unit at least 10 units away (`mindistanceownunitovershooting`, unitprefs.txt).
- So a gun happily fires over a ridge or over a friendly line sitting on a slope. The shell meets the ground short of the target, near that line.
- Direct-path hits skip own-side blocks (Projectile.cs ~345). But the fragment rays from `Projectile.GetExplosivesHits` (~298–318) have no alliance check, so the burst hits your own troops.

**Fix.** Friendly fire stays on; the gun just won't take the shot. When an artillery gun picks a target, `FireBullet` asks `CheckOwnUnitInWay` once per candidate enemy unit. A postfix there traces the shot's arc the same way `Projectile` does: a sine arc whose peak is `weapon.trajectorypeak / firerange × distance / battlefieldrealdimensionfactor`. The target counts as blocked if either:
- the arc passes through a friendly company block within `ArtilleryNearDistance` of the gun (default 40 units, about 250 m), or
- the arc meets the ground short of the target with a friendly block within fragment range of that point (`projectileweight × projectileweightratiorange`, at least 2 units). A landing within that distance of the target counts as a normal shot.

A blocked target is skipped exactly like vanilla's own-unit-in-way case, so the gun picks another target or holds fire.

Limitations:
- The trace uses the aim point without scatter, so a wide miss can still land near friends.
- Solid shot's bounce after landing isn't traced.

### Attack moves wipe combat behaviour – `KeepCombatBehaviourOnAttackMove`
**Bug.** `SetMovementMode(3)` (attack move) sets `combatbehavior = 0` for infantry and cavalry (Regiment.cs ~15828). Mode 3 is entered by:
- the Assault order
- mounted Advance (converted to mode 3)
- an infantry right-click while Charge is on
- the end of every tracked charge

The effects:
- Cavalry drop "charge" after their first charge.
- Infantry drop "engage" (3) the moment they move.
- The fire-one-volley-then-charge logic in `UpdateChargeTarget` needs mode 3 and behaviour 3 together, so it is unreachable.

**Fix.** A prefix/postfix pair keeps behaviours 3 and 6 across that call. Range behaviours 0–2 are still reset to close range when attacking, as vanilla intends.

### Cavalry Charge-off / Evade-off buttons do nothing – `CavalryButtonOff`
**Bug.**
- Both buttons send behaviour 5 (PanelHandler.cs ~3146/3198).
- `SetCombatBehavior` rejects 5 for every non-artillery unit (Regiment.cs ~13212).
- The button changes state but the unit stays in 3, 4 or 6.

**Fix.** For cavalry, 5 is mapped to 0 ("fire normally"). Nothing in the unit logic treated 5 specially anyway.

Not changed: cavalry start a battle in behaviour 6, where right-click always charges and there is no fire order. Toggling the button off and on gives 3, where right-click fires and Alt charges. Behaviour 6 is never read by the unit logic – it is purely a UI flag – so mapping the starting 6 to 3 would be a one-line patch if you prefer that control scheme.

### Skirmisher jitter when the enemy gets between them and their brigade – `SkirmisherFallback`
**Bug.** `CheckFallbackDueToEnemyClose` (Regiment.cs ~19982) runs every cycle for skirmishers, scouts, screening units and evading cavalry.
- It triggers when the nearest enemy is within `minimumdistancetonextenemyunittofallback` (26).
- The fallback point is 31 m from the enemy, on the line from the enemy towards the parent brigade (~20037). The unit's own position isn't used.
- If the unit is between enemy and parent, the point is behind it and everything works.
- If the enemy is between them, or off to the side, the point is on the far side of the enemy. The unit never gets clear.
- Every time the target drifts 5 m (`triggertorenewfallback`) it gets a new path and a new facing – the spinning.

**Fix.** A prefix detects the bad geometry: either the vanilla target doesn't lie away from the enemy as seen from the unit, or the enemy is closer to the parent than the unit is. In that case it uses the same construction on the line from the enemy through the unit itself – straight away from the enemy. Normal geometry and the in-melee case are left to vanilla.

### Routed detachments run through enemies to their brigade – `RoutedReattachAvoidsEnemy`
**Bug.**
- `Rout()` plots an enemy-avoiding path: a `Retreat.RetreatOrder` turns on enemy NavMesh obstacles, waits two frames for them to carve, then paths.
- But every cycle `CheckRoutUpdate`/`CheckRoutUpdate2` give a routed unit with a parent a reattach order.
- `CheckForReattachmentOrder` then paths straight at the parent with a plain NavMesh path (Regiment.cs ~20629), overriding the rout path.
- Routed units still trigger melee on contact, so they take casualties on the way through.

**Fix.** While a routed unit is inside `CheckForReattachmentOrder`, its `RegimentSetPath` call is replaced by a `Retreat.RetreatOrder` to the same destination – the mechanism couriers use to path around enemies. Running back to the parent is kept, since that part looks intended.

### Column steps back before following a new path – `ColumnBackStep`
**Bug.**
- The regiment transform is snapped to block 0 every cycle (`AdjustRegimentParentPositions`, Regiment.cs ~25782).
- In column, block 0 sits one row behind the head; the colour block is at the front (`ChangeRegimentFormation`, ~26516).
- A new path starts at the regiment position and lays out each block's slot for the first corner behind it. So every block's first target is one row (`blocksize` × 10 = 2.5 units, about 16 m) behind where it stands.
- `RegimentMove` only advances a block once it reaches its current corner. So the whole column marches back a row, turns, then advances.
- In line, block 0 is front-centre, so there is no offset – that's why it shows mainly in column.

**Fix.** A postfix on `RegimentSetPath`: for a fresh single path, blocks still aiming at corner 0 skip to corner 1. It is skipped when another prefix replaced the call.

### Charge end loses waypoints – `ChargeEndRestoresAllWaypoints`
**Bug.** When a tracked charge reaches its target, `UpdateChargeTarget` restores the waypoints the unit had before charging. It does this by calling `RegimentSetPath(newpath: true)` once per waypoint (Regiment.cs ~13383). Each call wipes the path before it, so only the last waypoint survives.

**Fix.** A prefix handles exactly that case first, with the same conditions: mode 6, target reached, target not routed. It appends the waypoints, using `newpath` only for the first. It then clears the charge target, so vanilla's block sees nothing to do.

### Found but not patched
- **Charge odds check (`UpdateChargeTarget`):** the strength weight is applied to both sides, so it cancels out.
- **Charge radius:** measured from the parent HQ. For independent units the parent is the root container, so their auto-charge never fires.
- **Counter-charge on contact:** skips block 0 (`<= 0` where `< 0` is meant, Regiment.cs ~17049).
- **Melee formation term:** `1 + defender_mod − attacker_mod` has no clamp, so e.g. a column attacked by skirmishers gets −0.75 – negative casualties.
- **Melee cavalry bonus:** still applies when the cavalry are dismounted.
- **Speed shock:** reads the defender's `isrouted` and position change instead of the attacker's.

### HQs crawl when moved on their own – `HqSpeedFix`
**Bug (battle).**
- Every unit caps its speed at its order-giver's "group speed": `speed *= min(groupspeed, 1)` (Regiment.cs ~24983).
- `UpdateGroupSpeed` (~31046) sets that to the slowest unit in the order-giver's whole group whenever the order-giver is an HQ. That includes an HQ moved on its own, when none of its units are moving.
- So an HQ (base speed 2) moved by itself drops to about half speed whenever its group contains unlimbered foot artillery (0.535). That's why it only happens "sometimes".

**Fix.** A postfix on `UpdateGroupSpeed` recomputes group speed from units actually moving under the same order. If none are, there is no cap.

**Bug (campaign).**
- Campaign speed is scaled by group training: `movementspeedtraining` runs from 0.7 at training 0 to 1.2 at training 1.
- An HQ with no units has nothing to average, so its training stays 0 and it always moves at 70 %.

**Fix.** An HQ with no units gets group training 0.6, a neutral 1.0 speed factor.

**Not the cause:** HQ cohesion or fatigue. HQs recover fatigue like any unit (`UpdateFatigue`).

### Historical/custom battles: some regiments ignore every group order – `CustomBattleReattach`
**Bug.**
- Battle files can place single regiments individually. These go into `BattleUnits.initialregimentoverride`.
- `AllocateUnitsAndObjectivesToAI` (BattleUnits.cs ~7566) then sets `permanentlydetached` on every such player regiment, with no condition.
- Group orders (`SetGroupFormation`, group moves) silently skip detached units (`IsAttached`, BattleUnits.cs:9108; `GetAttachedUnitsReg` excludes them). Those regiments ignore their brigade from the first order on.
- Different battle files list different regiments.
- Campaign battles always write the group flag (ImportExportUnitData.cs:499), so they are not affected by this path.

**Fix.** Such player regiments are re-attached when their parent is an HQ and they are not under AI control.

**Also found (not changed).**
- Group formation orders only reach three levels below the ordering HQ (`SetGroupFormation`, `PerformActionForGroupForAllUnitsOnLevel`). An army-level formation order therefore never reaches the regiments: army → corps → division → brigade → regiment is four levels.
- A unit whose campaign morale is below the crack level starts the battle "broken" (BattleUnits.cs:11024). Its morale is pinned just above the crack level, so any loss routs it, and routed units are dropped from group orders. The morale tooltip says "Unit started broken".

## Units ignoring move/formation orders near (invisible) enemies – `OrderInterruptionVisibleOnly`, `OrderInterruptionStaleClear` (src/OrderInterruption.cs)

**Report.** Career DLC battles: some units don't follow plain move or formation orders, from the very first order. Different units each battle, always the same units within a battle. Advance, charge and retreat orders work. It happens with no enemy visible or near.

### What goes wrong

Vanilla has a "path interruption" system for groups making a plain move. When a moving unit meets the enemy, the whole group stops at fire range and is marked `pathinterrupted`. It lives in `Regiment.CheckEnemyContact` (Regiment.cs ~8197–8445). It only applies to plain moves (`movementmode == 0`, combat behaviour 3 or `movementmode == 3`). That is why advance, fall back, charge and retreat are unaffected. Three things make it misfire on player units.

**1. Hidden and routed enemies count (Regiment.cs ~14807, ~13660, ~13969).**
- Every distance test uses `GetClosestEnemyUnit(float, …)`. It reads `unitrange.closestenemyunitfar`, which `UpdateUnitRangeFast` fills with no fog-of-war test. The lists next to it (`temp_enemyinrange`, `temp_enemyinfirerange`) do check `!iswithinfog`.
- So an enemy hidden in woods, or a single skirmisher company measured block by block (`usesinglecoys`), triggers the stop.
- Distances are in map units of 6.3 m (battleprefs.txt:62–63):
  - contact is first checked at 100 units, about 630 m (unitprefs.txt:989–990);
  - the group stops at 0.9 × fire range, about 290 m for a Springfield;
  - "in contact" lasts until no enemy is within 150 units, about 945 m (unitprefs.txt:991–992).
- `closestenemyunitfar` is only replaced when a new candidate is 7 units closer (~13969). It can therefore stay on an enemy that has since routed or been deactivated.

**2. In career battles the flag is never cleared (Regiment.cs ~8245; BattleUnits.cs ~7571; AIBattle.cs ~5194).**
- `pathinterrupted` is cleared in only one place: by a unit that has the flag, has `groupaiobject == itself`, and has `combatstatussubordinates <= 0` (no subordinate within 150 units of any enemy, fog ignored).
- The stopped group is the unit that gave the last moving order (`unitwhogavelastmovingorder`). For the player that is whatever was ordered: a brigade, a division or a single regiment.
- At career battle start, `AllocateUnitsAndObjectivesToAI` calls `ChangeStance` on the player's command. That calls `AIBattle.AddUnitToPool(…, overwriteaigroups: true)`, which sets `groupaiobject` to the command's top HQ for every unit in the command. The DLC block then re-applies the same assignment (~7576–7584).
- From then on, no brigade or regiment the player orders is its own `groupaiobject`, so once it is interrupted it stays interrupted for the whole battle. (Outside career mode each brigade is usually its own `groupaiobject`, so only regiments ordered on their own are affected.)

**3. A new order doesn't reset it (BattleUnits.cs ~13796; Regiment.cs ~8603, ~8265, ~22210; BattleUnits.cs ~14273).**
- `SetWaypoint(clearinterruptionpaths)` calls `ClearPathInterrupted`, which only empties saved paths. The flag stays set.
- While the flag is set:
  - an *unselected* unit on a plain move is stopped and turned to face the enemy whenever any enemy, hidden or not, is within 0.8 × its fire range (~8265). Regiments under an HQ that you order are unselected, so group and formation orders are the ones that break;
  - group formations are laid out unsorted (`if (grpreg.pathinterrupted) sortunits = false`);
  - `unitwhogavelastmovingorder` is no longer updated by new orders (~22210).

**Why it looks the way it does.**
- The first order of the battle brings some regiment within about 290 m of an enemy the player can't see. Its group is stopped and re-ordered to form up at that regiment's position (`SetWaypoint` at ~8434).
- Because of point 2, that group stays interrupted for the rest of the battle. Which group that is depends on deployment and on where the hidden enemies are, so it differs from battle to battle.

### The fix

1. **Visible, fighting enemies only (`OrderInterruptionVisibleOnly`).**
   - Applies while `CheckEnemyContact` or `SetInterruptedStatusOfSubordinates` runs (a `[ThreadStatic]` depth counter set by a prefix and cleared by a finalizer).
   - During that time, `GetClosestEnemyUnit(float, bool, bool)` rejects a cached closest enemy that is routed, inactive or at zero strength. For player-controlled units with fog of war on, it also rejects one that is hidden (`iswithinfog`).
   - When the cached enemy is rejected, the closest acceptable enemy is taken from `unitrange.enemyinrangereg` instead, using vanilla's own distance measure. That list is already fog-filtered and covers at least 150 units.
   - The AI keeps its fog-free view. Only the routed/dead filter applies to it.
2. **Fresh orders clear a stale flag (`OrderInterruptionStaleClear`).**
   - Applies to player move orders (`SetWaypoint` with `showmovementoptions`, which only the UI uses) and player formation orders (`SetFormation` without `exitifsameformation`, which the AI always sets).
   - It runs before vanilla handles the order. It clears `pathinterrupted` on each unit the order reaches, unless a visible enemy is within that unit's own vanilla stop distance (0.9 × fire range, at least 35 for HQs, at most 100). In that case the unit really is in contact and vanilla behaviour stays.
   - Because the flag is cleared before `RegimentSetPath`, formations are sorted again and `unitwhogavelastmovingorder` updates.
   - A cleared unit that then meets a visible enemy is stopped at fire range by vanilla, as intended.
3. **Stuck units clear themselves (`OrderInterruptionStaleClear`).**
   - After `CheckEnemyContact`, a player unit with the flag set that vanilla can never clear (`groupaiobject != itself`) clears its own flag once no visible enemy is within the vanilla cancel distance (150 units).
   - Units that are their own `groupaiobject` are left to vanilla's clear, which also resumes the interrupted path.

"Player-controlled" means: player alliance, no AI stance, not AI vs AI, and in career battles `dlcw_isundercommander`. Friendly AI units in career battles keep vanilla behaviour.

### How the player can confirm

- **Career battle, fog of war on:** order a brigade or division through woods towards an area where enemy skirmishers or cavalry may be hidden.
  - Vanilla: the group halts and reforms around one regiment, and later group or formation orders to it are ignored by the unselected regiments.
  - With the fix: it keeps moving until an enemy is visible within fire range.
- **A brigade that did make contact:** pull it back out of sight of the enemy, then give a new move or formation order. It should obey normally.
- **Debug log:** with the game's debug log on, "set path interrupted: True" lines should name a visible enemy unit. "set path interrupted: False" should follow when a unit is re-ordered away from contact.

### Limits

- A unit still in real, visible contact keeps vanilla's contact behaviour:
  - unselected regiments of a group stop at 0.8 × fire range;
  - give the order to the regiment itself, or use advance or fall back, to move it anyway.
- Vanilla's own clear on the top HQ (`groupaiobject == itself`) still stops the group and restores the path that was interrupted. If the player has since ordered sub-units elsewhere, that restore can override those orders. Not changed.
- Fog uses the player-side `iswithinfog` flag. With fog of war off, only the routed/dead filter is active.
- The artillery variant of the stop (`GetClosestEnemyUnitPerTypeAndFireRange`, ~8269) reads a different list and is not fog-filtered.
- Tested only for patch application against the real DLLs, not in a running battle.

## Surrender and pursuit (src/Surrender.cs)

Battle time (`GameVars.currenttimefromstart`) is in hours: the clock hour plus 24 × days passed (BattleUnits.cs:8630).

### Melee surrender almost never triggers – `Surrender.CumulativeMeleeSurrender`
**Bug.**
- Vanilla's 0.22 h surrender countdown is armed only at the instant a unit routs while in melee (`Regiment.Rout`, ~14706).
- `UpdateSurrender` (~15007) clears it if the unit has been out of melee for 20 % of the countdown (about 2.6 game-minutes), and nothing re-arms it. A routed unit that slips away once and is caught again can never surrender this way.
- It also needs unbroken contact, and the unit must still be in melee with a non-routed enemy at the exact moment the countdown ends.
- Routed units run at full speed – the fatigue speed penalty skips routed units (~24570) – and infantry drops a charge as soon as its target routs. So in practice only cavalry could ever hold a broken unit for 13 minutes.

**Fix.** A postfix on `UpdateSurrender` keeps a per-unit total of melee time while routed. It builds while the routed unit is in melee with a non-routed enemy, and drains at `SurrenderMeleeDecay` (default a quarter of the build rate) while out of contact. At `SurrenderMeleeHours` (default 0.1 h = 6 minutes) the unit starts the vanilla surrender sequence: it walks toward the enemy showing surrender sprites, fades, then calls `Surrender()`, which handles POWs, the commander and so on. The vanilla countdown still works alongside.

### Feature: routed units can be run down – `Surrender.RunDownSurrender`
Every ~36 s of battle time, a routed unit checks for non-routed enemy combat units (not artillery or couriers) within `RunDownRange` (default 10 units, ~63 m) that are faster than it by `RunDownSpeedMargin` (default 1.1×).

Speed = `unitbasicspeed[type, mounted] × runningspeedfact[type]`, with the vanilla fatigue penalty applied to the pursuer only (routed units ignore fatigue in vanilla).

If such pursuers exist, the unit surrenders with probability `1 − exp(−rate × 0.01 h)`, where

```
rate = RunDownChancePerHour (3) × size × (0.5 + fatigue) × (1 + loss ratio) × (1.5 − experience)
size = min(1, total pursuer strength / routed strength)^1.5
```

The size term is what keeps small cavalry from bagging big brigades:
- 300 cavalry chasing a 1,500-man brigade: size 0.09.
- 750 chasing 1,500: size 0.35.
- Equal or larger pursuers: size 1.

For an average case (equal size, fatigue 0.5, 30 % losses, experience 0.5) the rate is about 3.9 per hour, so a unit kept under pursuit surrenders in roughly 15 minutes on average. Because infantry and routed infantry move at the same base speed, this is in practice cavalry running down foot troops and gunners.

### Infantry gives up the chase – `Surrender.InfantryPursuit`
**Bug/design gap.** In `UpdateChargeTarget` (~13350) a charge whose target routs is dropped at once unless the charger is cavalry. Infantry stops where it is and can never close on a broken unit.

**Fix.** A prefix keeps infantry (unittyp 0) tracking a routed charge target, using vanilla's own cavalry tracking (re-path when the target has moved more than 5 units). It does so while:
- the target is within `maxchargeradius` of the charger (65 units)
- the charger is not routed, and its fatigue is below `InfantryPursuitMaxFatigue` (default 0.6).

On reaching the target, or exceeding either limit, vanilla takes over and the charge ends.

### Limits
- The melee totals are kept in memory only; a battle save/load resets them.
- Numbers are first guesses; all are in the config under `[Surrender]`.

## Fleet disintegration loop (src/FleetDisintegration.cs)

Config: `[Campaign] FleetDisintegrationLoopFix` (default on), `FleetDisintegrationRepeatDays` (default 7), `FleetDisintegrationScuttleShips` (default off). Code: `src/FleetDisintegration.cs`.

### Player report

A fleet that disintegrates in the campaign and is respawned at a harbor where an enemy fleet already sits keeps disintegrating and teleporting every few hours, forever.

### How fleet disintegration works (vanilla)

All refs are to the ILSpy output in `gt/src`.

1. **Something makes the fleet retreat.** Every campaign update calls `Regiment.CheckEnemyContactCampaign` (Regiment.cs:3230 → 6648). Fleets are sent to `SetRetreatToDepot` by:
   - `BattleUnits.EstablishCampaignBattle` when the enemy's morale is below `minimummoraleforunitstoparticipateinbattles` (BattleUnits.cs:2942–2948) or its side is too weak to fight (3087–3094);
   - the post-battle retreat of the losing side (BattleUnits.cs:15252);
   - a failed fort bombardment (Autocalc.cs:1416, 1456);
   - **renewal**: a fleet that is `onretreat`, has no path left and still has any enemy unit within `GetClosestEnemyUnitReg(buglerange or enemy buglerange × 1.1)` retreats again from its own position (Regiment.cs:6688–6698). For fleets the "still on enemy territory" part is forced off (6688–6691), so only the enemy-nearby test matters.
2. **Choosing a destination** – `Regiment.SetRetreatToDepot`, fleet branch (Regiment.cs:7062–7154):
   - `GetClosestHarbor(fromposition, alliance, excludeblocked:false, sea, river, checkenemyclose:true, checkdirection, minimumdistance:num2)` (7103). Only own-alliance harbors are considered (owner filter in `GetClosestIIP`), at least `num2` (≥ 1.5 × bugle range) away.
   - The enemy test inside `GetClosestHarbor` (BattleUnits.cs:4442–4468) only rejects a harbor if an enemy fleet/garrison is *closer to the harbor than the fleet is* **and** within 45° of the line harbor→fleet. An enemy anchored right on the harbor, but on the "far side", passes. It also rejects every harbor that is not within 45° of `checkdirection`; on the renewal path `fromposition` is the fleet's own position, so `checkdirection = GetAngle(p, p) + 180` is a meaningless fixed angle and most harbors get rejected.
   - If that returns nothing, the fallback (7104–7118) asks again with `checkenemyclose:false` – **no enemy test at all** – and calls `DisintegrateUnit(closestHarbor)`; if there is no harbor at all, `DisintegrateUnit()`.
   - Two more `DisintegrateUnit()` calls (no harbor) when the harbor or the path is on land (7139, 7152).
3. **`DisintegrateUnit`** (Regiment.cs:6730), fleet branch (6751–6800):
   - with a harbor (`preventfleetharbor` is the *destination*, not an exclusion): message "fleet survives" (6759), readiness → 0 (6771), every ship `SendToHarbor(harbor)` for repairs (6780–6786), the fleet is teleported with `SetPosition(harbor)` and stopped (6787–6788). It stays `onretreat = true` with no path.
   - without a harbor: message "unit disintegrates" (6755), deselect, `BattleUnits.DeleteFleet(this)` (6797 → BattleUnits.cs:11430): every ship goes to the alliance ship pool (`MoveShip(ship, alliance)` 5148 → `Vessel.AddToPool` Vessel.cs:500), then `DeleteUnit` (12879) removes the commander from command (12910) and destroys the unit.

### Why it loops

After the teleport the fleet is `onretreat`, has no path, and an enemy is within bugle range of the harbor. On the next campaign update the renewal rule (Regiment.cs:6692) fires `SetRetreatToDepot` again from the harbor. The direction-filtered search fails, the fallback picks the closest own harbor beyond `num2` without looking for enemies – typically the harbor it just came from, or another occupied one – and `DisintegrateUnit(harbor)` teleports it again. Nothing records that the fleet has just done this, and there is no rule that ever turns "no safe harbor" into "fleet lost". The fleet alternates between occupied harbors every few hours, with readiness pinned at 0 and the ships repeatedly sent for repairs (with repeated resource consumption in `SendToHarbor`, Vessel.cs:861–887).

The game has no per-unit "last disintegrated" timestamp. Readiness is set to 0 (6771), but readiness also drains to 0 from battles and sailing (Regiment.cs:6067–6085), so it cannot be used as a marker.

### The fix

Harmony prefix on `Regiment.DisintegrateUnit`, only for fleets (`unittyp == 17`) called with a harbor (the teleport path):

1. **Loop breaker.** A static `Dictionary<Regiment, float>` stores the campaign day (`GameVars.currenttimefromstart × 365.25`, which in the campaign is derived from the calendar date, CampaignController.cs:495) of each fleet's last teleport. A second teleport within `FleetDisintegrationRepeatDays` removes the fleet instead.
2. **Safe destination.** A harbor is unsafe if it is not owned by the fleet's alliance, or if any active enemy campaign unit (`BattleUnits.campaignunitlist`: armies, garrisons, fleets with ships) is within `max(fleet bugle range, enemy bugle range × 1.1) × 1.25` of it – the same radius that triggers the renewal, plus a margin. If vanilla's target is unsafe, the closest safe own harbor (to the fleet) on the same kind of water (sea/river, tested like `GetClosestHarbor`, BattleUnits.cs:4430–4441) is used instead.
3. **No safe harbor → removal.** If there is none, the fleet is removed.

"Removed" means the prefix sets `preventfleetharbor = null` and lets vanilla run its own no-harbor branch: "unit disintegrates" message, `DeleteFleet` (ships to the alliance pool), `DeleteUnit` (commander unassigned, unit destroyed). No objects are deleted by the mod itself. With `FleetDisintegrationScuttleShips = true` the ships are first sunk with the game's `Vessel.SinkShip` (Vessel.cs:806, as in battle) and the fleet's ship array is cleared, so they are lost rather than pooled.

Calls to `DisintegrateUnit()` without a harbor are left unchanged (only the dictionary entry is dropped).

### Limits and untested assumptions

- Not tested in a running campaign; only verified that the patch applies under Mono against the real DLLs (harness) and builds.
- The dictionary is not saved. After loading a save, a fleet gets one "free" teleport before the timer applies again. The no-safe-harbor test is stateless, so the main loop (occupied destination) is still broken right after loading.
- A fleet that legitimately escapes twice within 7 days (two separate lost battles with no retreat path) is removed on the second. Set `FleetDisintegrationRepeatDays` to 0 to rely on the safe-harbor test only.
- The safety radius uses bugle ranges; an enemy just outside it could still approach and trigger a renewal later. The day timer covers that case.
- Normal (sailing) retreats chosen by `GetClosestHarbor` with `checkenemyclose:true` are not changed – the weak enemy test there can still send a fleet towards an occupied harbor, but it sails there instead of teleporting, so it does not produce this loop.
- Pooled ships (default) go to the closest own harbor (`Vessel.AddToPool`); the player or AI may form a new fleet from them. That is vanilla behaviour for a fleet that disintegrates with no harbor.
- The game has no global "ships lost" statistic (sunk ships are only counted per battle, Autocalc.cs:1875), so scuttled ships are only logged.

## Auto-resolve fixes (src/Autoresolve.cs) – `AutoresolveFixes`

All four are IL transpilers on `Autocalc`. Each logs how many spots it changed at startup – check `BepInEx/LogOutput.log`. A count of 0 means a game update moved the code and that fix is inactive.

### Sharpshooter and Zouave perks are swapped
**Bug.**
- In the battle code, perk 1 is Sharpshooters (range and accuracy, Regiment.cs ~9413, ~10279) and perk 2 is Zouaves (fatigue factor ~23792, running ~4278, no high-fatigue morale penalty ~19717).
- Auto-resolve swaps them:
  - `FightUnit` gives the sharpshooter firepower bonus (`sharpshooterperkinfluenceautocalc`) to units with perk 2 (Autocalc.cs ~3152).
  - `FightUnit` exempts units with perk 1 from the fatigue-morale penalty (~3319).
  - `UpdateFatigue` applies `perk_zouaves_fatiguefactor` to units with perk 1 (~3440).
- So in auto-resolved battles Zouave brigades shoot like sharpshooters, and sharpshooters march like Zouaves.

**Fix.** In those two methods, the perk id passed to `HasPerk`/`GetPerkLevel` is swapped (1 ↔ 2). HQ-level perks (`HasPerkParent`, ids 10/11/15/17) are untouched.

### Entrenchment always uses side 1's engineering points
**Bug.**
- When the target's side is defending, `FightUnit` reduces casualties by `engineeringpoints[1] / 100 × militaryexperience` (Autocalc.cs ~3149).
- The index is hardcoded to 1, whichever side is defending.
- So side 0's defenders get side 1's entrenchment – none at all if side 1 has no engineers, or a lot from the enemy's engineers.

**Fix.** The index becomes the defending unit's side, i.e. the same side whose stance is checked.

### Counter-battery chance indexed by side, not stance
**Bug.**
- `DoArtilleryAction` reads `cbstancepairs[side, enemySide]` (Autocalc.cs ~3066).
- The table is 4 × 4, one row/column per stance (assault, attack, defend, retreat), so with side indexes only the assault/attack corner is ever used.

**Fix.** It reads `cbstancepairs[usedstance[side], usedstance[enemySide]]`.

### Last unit never picked
**Bug.** `PickUnit`, `PickRandomUnit`, `PickTargetVessel`, `CalculateArtilleryOverruns` and `DoArtilleryAction` choose with `Random.Range(0, Count - 1)`. Unity's int overload excludes the upper bound, so:
- the last entry is never chosen
- with two candidates, only the first is ever chosen
- e.g. a side's last artillery battery is never counter-battery targeted

**Fix.** Calls whose upper bound is "… − 1" become `Random.Range(min, max + 1)`.

The same off-by-one exists outside auto-resolve (GameVars.cs ~3260, Policies.cs ~872/1091, BattleUnits.cs ~5006, AICampaign.cs ~3558). Those are not patched.

### Weapon quality: auto-resolve only uses firepower – `[Autoresolve] WeaponQuality`
**Bug.**
- `FightUnit`'s weapon term for infantry and cavalry is `√firepower[0]` (Autocalc.cs ~3157). Rate of fire, range and accuracy don't enter it.
- Experience is already in the formula (`expinfluenceonenemycasautocalc`), as are cohesion and fatigue.
- Result: smoothbore muskets (FP 1.7, √ = 1.30) beat rifle-muskets (FP 1.0), and Henry/Spencer repeaters (FP 0.6, √ = 0.77) come out worst.

**Sanity check first: why the battle engine also likes muskets.** In battle, scatter is `distance / max(A, 1)` (Projectile.SetTargetNoise), where A blends weapon accuracy with experience and morale: `(acc + 0.66·exp + 0.25·morale) / 1.91`. A weapon below accuracy 1.0 can never push A above 1. Even a 1.2-accuracy Whitworth with veteran crew only reaches A ≈ 1.10. So in battle, accuracy barely matters and damage per hit is firepower. Rifles get their edge from range; repeaters get theirs from rate of fire. Auto-resolve drops both, which is why it is so much more musket-friendly than real battles. The AI's own weapon valuation (`range × ROF^0.2 × FP/2 × accuracy³`) is no good here – it rates the Whitworth 6.5× a Springfield.

**Fix.** The small-arms term becomes `√firepower × √(ROF factor × accuracy factor × range factor)`. A Springfield M1855/61-class weapon scores 1 on each factor:
- **ROF:** `(ROF / 3)^0.7` (0.5 before 1.0.6).
- **Accuracy:** by default the battle blend and clamp, using the unit's experience, morale and fatigue, squared because scatter is two-dimensional. Optionally raw `accuracy / 0.9`.
- **Range:** `(range / 350)^0.5` at defend distance, half that exponent at attack distance, none at assault.

All exponents are in the config. Relative to a Springfield, with the AOM weapons.txt (average unit, defend / assault distance):

| Weapon | Vanilla | New (defend / assault) |
|---|---|---|
| Springfield M1842 musket | 1.30 | 1.23 / 1.30 |
| Mixed smoothbores | 1.30 | 1.21 / 1.29 |
| Enfield P1853 | 1.00 | 1.04 / 1.03 |
| Sharps rifle | 1.00 | 1.47 / 1.38 |
| Henry / Spencer rifle | 0.77 | 1.37 / 1.39 |
| Spencer carbine | 0.84 | 1.48 / 1.50 |
| Whitworth | 1.00 | 1.14 / 1.00 |
| Springfield M1866 | 1.00 | 1.54 / 1.41 |

Existing BepInEx config files keep the value they were created with; set `RofExponent = 0.7` in `aom.bugfixmod.cfg` (or delete the file) to pick up the new default.

Muskets still edge rifle-muskets because the data gives them 70 % more firepower per hit. That's a weapons.txt choice, and it matches how real battles play under the accuracy clamp. Range alone can't close that gap sensibly – `RangeExponent` would need about 2.4 to level them at defend distance – so if you want rifles ahead, lower musket firepower in weapons.txt.

Artillery keeps vanilla's per-distance firepower. Machine-gun ROF values (40–200 a minute) aren't on a comparable scale and would dominate.

**Also fixed here – 6-pounders do nothing.** Both branches of that term test `weapon > 0`. Weapon 0 is the M1841 6-pounder, so those batteries got a firepower term of 0 and inflicted no casualties in auto-resolve. The test is now `weapon >= 0`.

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

## POW camps, parole and raids (src/PowCamps.cs)

**How it works in vanilla.** After a battle (`WriteBattleResultToMonument`, ImportExportUnitData.cs ~3140) each beaten group's captured and missing men, plus 15–30 % of the loser's wounded (0–7 % of the winner's), go to the captor's nearest finished POW camp anywhere on the map, whatever its fill level. If the captor has no camp at all they are paroled: meant to return at once to their own side's recruiting pools (vanilla has the sign reversed, see 0), spread by each state's share of volunteers and scaled by the re-enlistment ratio (23.5 % at 0 support to 80 % at full support, ±10 %). They come back as recruits, not to their units. There is no exchange and no release over time. A camp over capacity (7,500 at level 1, 33,000 at level 2) moves the overflow to the owner's nearest camp with room; if it is still over, the prisoners' side gains support in its own states (`supportincpowcapacity`, about +13 points a year for 15,000 men in a level-1 camp). When a camp's ground changes hands to an enemy (`ChangeOwnership`, CBuilding.cs ~524) everyone inside is released the same way as parole – this part works as intended. Captured generals are separate: each returns with 60 % (low rank) to 86 % (high rank) probability per year, camps or not.

### 0. Released prisoners shrink their own side's recruiting pool – `PardonAddsRecruits`
**Bug.** `CBuilding.PardonPrisoners(number, alliance)` (~2769), used for parole and for prisoners freed when a camp is captured, calls `Nation.AdjustRecruitingPool(+amount)`. That function counts men *recruited*: available volunteers = potential − `volunteersrecruited` (GameVars.cs ~4879), and each recruited man also removes 5 people from his state's town workforce. Raising a unit uses +strength; disbanding uses −strength. So every paroled or liberated prisoner cost his own side a recruit and workforce instead of returning one.
**Fix.** Same distribution across states and the same re-enlistment ratio, with the sign reversed, never taking a state's recruited count below 0. Raids that free prisoners (below) use the same routine.

### 1. Prisoners sent to captured camps – `PowCampPriority`
**Bug.** The nearest camp is used regardless of capacity or where it stands, so a camp just taken in enemy territory – usually the closest to the front – fills first and is the one most likely to be retaken, releasing everyone. This is also why a camp can hold prisoners right after it was captured: release on capture works, but the next battle refills it.
**Fix.** New prisoners go to the nearest finished camp in this order: room and in a state loyal to its owner; room in a captured (not loyal) state; full and loyal; full and captured. Overflow transfers use the first two tiers.

### 2. Parole button empties the camp – `ParoleExcessOnly`
**Bug.** The camp panel's Parole button calls the same routine as capture and releases every prisoner.
**Fix.** On an over-capacity camp the button paroles only the prisoners above capacity, in proportion across the prisoners' alliances. Within capacity it paroles everyone, as before. Capture still frees everyone.

### 3. Raids can't free prisoners – `RaidFreesPows`, `RaidPowFreeShare`
**Feature.** Raids (cavalry "raid" order) only lower a building's condition, and a POW camp's condition does nothing. Now every time a raid damages an enemy camp, the share of prisoners freed is the condition lost × `RaidPowFreeShare` (default 0.5: half the prisoners by the time the camp is at condition 0, more if the raid continues after repairs). Raid damage already shrinks with the guarding efficiency of nearby enemy units, so a guarded camp resists. Freed men go to their side's recruiting pools like paroled men. Historically such raids were tried (Kilpatrick–Dahlgren on Richmond, Stoneman toward Macon and Andersonville, both 1864) and mostly failed, so the default is moderate.

### 4. Raid effects snowball – `RaidTimerFix`
**Bug.** `UpdateRaids` (Regiment.cs ~4698) sets `lastraidupdate` once and never advances it, so every update applies loot, enemy stock losses, trade-warfare costs and support changes for the whole time since the raid began. Effects grow every tick until a save/load resets the private timer.
**Fix.** The timer moves to now after each update and resets when the raid order ends.

**Not changed:** building damage from raids is applied per update rather than per unit of time (`raiddamage` is documented "per year" but isn't multiplied by elapsed time), so how fast a raid wrecks a building depends on update frequency.

## Men leaving the army (src/Manpower.cs)

**How it works in vanilla.** Each state has a recruit pool = potential − men recruited, and every man recruited also removes 5 people from its towns' workforce. Only disbanding a unit gives men back (its current strength). Deserters (UpdateCampaignUnitStats ~6245) move from strength to `losses[2]`; men who don't re-enlist when contracts expire (CheckContractExpiries ~4600) simply vanish (their weapons go back to stock). Neither returns anyone to the workforce or the pool, and no deserter ever comes back.

### 1. Deserters handed to the enemy as prisoners – `DesertersNotPrisoners`
**Bug.** `losses[2]` holds both campaign desertions and men missing in battle. After every battle `WriteBattleResultToMonument` (~3175) sends each involved unit's whole `losses[2]` to the enemy's POW camps (or paroles it) and zeroes it; `UpdateGroupStrength` (~11388) then restores it from the smoothed `missingavg`, so roughly the same deserters are handed over again after the next battle. Prisoner counts and battle reports are inflated, and with paroles fixed (PardonAddsRecruits) the duplicates would even turn into recruits.
**Fix.** The mod records each unit's `losses[2]` at every campaign update outside battle. Around the battle result each unit's `losses[2]` is cut to what it gained since (the battle's own missing) and the desertion tally is restored afterwards. Tactical battles report only wounded and captured back to the campaign, so for them nothing in `losses[2]` counts as captured. Just after loading, before the first update, the whole tally is treated as desertions.

### 1b. The per-state Deserters figure balloons – `DeserterStatistic`
**Bug.** `Nation.deserters` (shown per state in the recruiting list) grows only at the end of a battle: `Autocalc.FinishLandBattle` (~4058) and `BattleUnits.SaveBattleResult` (~5964) add each of the player's units' whole `losses[2]` – its running desertion tally, which vanilla keeps restoring (see above). Every battle adds all of a unit's past deserters again, while desertions between battles are never counted as they happen.
**Fix.** Those battle-end additions are undone, and deserters are counted when they desert (player's units only, as in vanilla). Figures already in a save stay inflated.

### 2. Departing men go home; some deserters come back – `DesertersGoHome`, `DeserterReturnShare`, `DeserterReturnRate`
**Fix/feature.**
- Men leaving at contract expiry return to their home state: its recruited count drops by that many, restoring workforce and recruit pool.
- Of each update's deserters, `DeserterReturnShare` (default 0.35) are returned to the army later; the rest go home at once. Historical guide: the Union Provost Marshal General's bureau arrested 76,526 deserters against roughly 200,000 recorded Union desertions (estimates run to 279,000), and both sides ran amnesties in 1863–65. Confederate figures (about 104,000 deserters) don't record returns, so the same share is used.
- Returning deserters wait in a pool per state and side and come back at `DeserterReturnRate` per year (default 3: on average about four months away). They fill under-strength infantry and cavalry units from their state, drawing rifles from stock where available; if no unit has room they go home.
- The pool is saved with each campaign save as `bugfixmod_absent.txt`. Older saves start with an empty pool.

## Trade and import pricing (src/EconomyFixes.cs)

**How trade works.** Each trade hub (IIP) pulls goods from its connected hubs every economy update. A good's attractiveness on a link is the price gap `relprice(here) − relprice(there) × tariff markup / trade-deal factor` (with ±10 % noise, scaled by the other hub's stock); the link's volume is shared between goods by attractiveness³ and limited by transport capacity and distance. Trade between two sides needs a trade agreement (set per side in the scenario's nations.dat and never changed in play). In the main scenario the Union and the Confederacy each have agreements with Britain, France, Spain, Mexico, Sea Trade and the Neutral States, but not with each other, so no trade crosses the front at all (the 10 % enemy-trade factor never applies).
- **Tariffs:** the markup is `1 + cap × slider`. The cap is set by the scenario and policies (Tariff Act 0.5, also −0.05 European relations once; Free Trade 0.3 for the Confederacy). Revenue is the same rate on the value of imports only; exports are untaxed.
- **Trade Deals project (id 121):** the trade-deal factor is `1 + 0.045 × level` (config label "project send envoys: increase in tradevolume" – misnamed). It applies to every link of the side's hubs, domestic included, so it raises domestic as well as foreign trade.
- **Send Envoys project (id 103):** only +0.05 relations per level.
- Two diplomacy-subsidy settings ("improved prices factor" and "intervention level per year of full diplomacy subsidies") are read but never used.

### 1. Tariff markup leaks onto domestic trade – `TariffPerConnection`
**Bug.** In `IIP.UpdateEconomy` (~941) the markup is set when the loop meets the hub's first foreign link and never reset, so every later link in that hub's list – domestic ones too – is treated as an import when ranking goods. No revenue is collected on them, but domestic flow out of port and border hubs is dampened, depending on link order.
**Fix.** A transpiler replaces the markup in the price-gap formula with a per-link value: 1 for same-side links, `1 + cap × slider` for foreign ones.

### 2. Union import prices follow Confederate relations – `ImportPriceRelationsPerSide`
**Bug.** `Weapon.GetProductionFigures` (~5280) computes the relations term for imported weapons as `(P(Britain) + P(France) − P(Spain)) / 3` in both branches of an `alliance == 1` check. These are intervention probabilities – Britain/France leaning Confederate, Spain leaning Union – so the Union got the Confederate figure: Europe warming to the Confederacy made Union imports cheaper too. The term maps 0 → 1.65× and 1 → 0.75× cost.
**Fix.** The Union gets the mirror image: Britain's and France's lean toward the Union minus Spain's lean toward the Confederacy, on the same chapter-threshold scale as the intervention probabilities. The Confederate figure is unchanged.

### Reference (not changed)
- **Workforce:** a map where towns add √(population) and companies subtract √(workers); blurred, floor 15 %, doubled by `workforceleverage` and capped at 1. Company output scales with it (slaves count as full workforce for their share). Town wealth is multiplied by 1.6 at zero available workforce and 0.9 at full, so labour shortages raise wealth. Each man a state has ever recruited (dead and deserted included) removes 5 people from its towns' population for this purpose.
- **Population:** follows the scenario's census curve (historical immigration is built in), plus a workforce term `(1/√availability − 1.2) × 0.05` per year – up to about +4 %/yr where labour is tight, about −1 %/yr where it is slack. People are created or removed, not moved between states. Some policies multiply state populations once. There is no separate immigration mechanic.
- **Relations** affect only intervention (deterministic once the "probability" reaches 100 %; scenarios 001/002 only) and import prices.

## Save references and end-of-day freeze (src/SaveAndEndOfDay.cs)

Source file: `src/SaveAndEndOfDay.cs`. Switches: `Battle.EndOfDayFreeze` and `Battle.SaveReferences`.
Line numbers point into the ILSpy output (`gt/src`).

### 1. Multi-day battle freeze (`EndOfDayFreeze`)

#### What goes wrong
`BattleUnits.CheckTimeIssues` (BattleUnits.cs:8620–8739) runs the night cycle through `eodcycle`:
0 fighting → 1 End of Day prompt → 2 night → 3 new day → 4 Troops Resupplied prompt → 5 deployment prompt → 6 deployment finalised.
While `eodcycle > 0` units can't move (Regiment.cs:24231), can't fire (Regiment.cs:9964), melee and cohesion are skipped, and the AI is off (AIBattle.cs:3878, 5805).

The only thing that puts it back to 0 is `BattleUI.SetActiveDeploymentPhase(false)` (BattleUI.cs:1236–1248), and it only does that if `deploymentmodeactive` is still true at that moment. The battle locks up in three ways:

- **Cease-fire**: `triggerendofday` (PanelHandler.cs:2541) sets `eodcycle = 6` straight away (BattleUnits.cs:8652–8656), with no deployment mode. The next nightfall normally picks this up (8660). But if the AI withdraws at night, `CheckAINightWithdrawal` returns early (8662), and `triggerendofday` is only cleared when the night ends (8640). So 6 can be left with nothing to end it.
- **DLC "no deployable group" branch** (8701–8720): `SetActiveDeploymentPhase(true)` followed by `(false)` happens before `eodcycle = 5`. The reset to 0 runs first and then gets overwritten with 5, and 5 moves on to 6 (8738) with deployment mode already off. The battle is stuck at 6 for good, which is the main reported freeze.
- **Stale prompt id**: states 1, 4 and 5 only move on when `panelhandler.prompt_panelopen == -1`. Only the list overload of `HidePanel` resets that id (PanelHandler.cs:7197–7206). Closing the prompt by another route (for example `ClosePanel.DoClosePanel` → `HidePanel(Panel)`, ClosePanel.cs:98) hides it but leaves the id set, so the cycle never advances.
- Related: during deployment (6 + `deploymentmodeactive`), several UI paths call `GameVars.SetGameSpeed()` without ending deployment (in-game menu close BattleUI.cs:881, timed-movement panel PanelHandler.cs:2721, cancelling an end-of-battle prompt BattleUnits.cs:5641). Time then runs while every unit stays frozen.

#### The fix
A Harmony postfix on `BattleUnits.CheckTimeIssues` (it runs every battle frame). It does nothing in the campaign, when `eodcycle == 0`, once the battle is ending (`endbattle >= 0`), or while a retreat timer is running. Each repair only fires after the bad state has lasted more than 1 real second, and each one logs a warning:

- **(a)** For `eodcycle` 1, 4 or 5: if `prompt_panelopen != -1` but the prompt panel is neither active nor in a visible state, it sets `prompt_panelopen = -1`. The vanilla state machine then carries on by itself.
- **(b)** For `eodcycle == 6` with `deploymentmodeactive == false` and `triggerendofday == false`, it does what `SetActiveDeploymentPhase(false)` would have done in that situation: `eodcycle = 0`, `deactivateshortcuts = false`, `showsupplylines = false`, and `aibattle[0].AICheckInitialPontoonConstruction()`. No panels need hiding. AI placement is skipped on purpose, because in this state units are already where they were.
- **(c)** For `eodcycle == 6` with deployment active while the clock is running, it pauses the game again. Deployment stays intact and the player ends it with play, as intended.

Why this can't cut a real phase short:
- States 1–5 are never forced. Only a prompt id whose panel is actually gone gets cleared.
- 6 with deployment active is never ended.
- 6 during a cease-fire (`triggerendofday == true`, fighting halted until night) is left alone.

#### Limitations and assumptions
- Not tested in game. Built only against the decompiled source.
- Cease-fire edge case: if `triggerendofday` stays true and the AI keeps withdrawing at night, (b) still won't fire. In vanilla that path ends the battle anyway (`SetEndOfBattle(2/7)`), and that is caught by the `endbattle >= 0` guard.
- `eodcycle` is not saved. Loading a save in deployment calls `SetActiveDeploymentPhase(true)` with `eodcycle == 0` (ImportExportUnitData.cs:6057), and that path was already fine.

### 2. Battle save cross-references (`SaveReferences`)

#### What goes wrong
- `battleunits.dat` writes each unit's references (courier source, `parentregiment`, `detachedregiment`, targets, AI group, and so on) with `SaveGameObjectRefs` as four lines: name, x, y, z (ImportExportUnitData.cs:6601, called at 6421–6576).
- On load, `GetRegGameObjectFromPosition` (7242) accepts a unit only if the name matches AND x/z round to the same 0.5 grid cell (`Tools.CompareVector3`, Tools.cs:4166). Anything else silently gives null.
- Positions are restored at 6667–6680, but while the per-unit block is being read, `ChangeRegimentFormation`/`StopRegiment` (7225–7227) run for units that come earlier in the file. A unit that later units refer to may have shifted by then. Rounding also fails for points near a cell edge.
- Detachments made by `BattleUnits.DetachCompany` (12701) are `Instantiate` clones. They share the brigade's commander id, are named `"<Commander>'s Detachment"`, and are linked only through `parentregiment`/`detachedregiment`. Nothing else in the save ties them to the brigade.
- `regiments.dat` writes the unit's hierarchy parent as a group index (ImportExportUnitData.cs:2124–2137). So a detachment that sits as a sibling of its brigade comes back under the brigade's group or, if it was saved at the top level, under no group / the HQ.
- When the link is lost:
  - the brigade's `detachedregiment` is null, so `DetachmentPossible` (BattleUnits.cs:12678) lets it detach again;
  - the detachment can't reattach (`ReattachCompany` returns -1);
  - the detachment shows the wrong symbol (Regiment.cs:20895);
  - on campaign return its strength and losses are never merged back (BattleUnits.cs:3508).

Casualties: each unit's own `losses[]`, `grouplosses`, and so on are saved per unit (ImportExportUnitData.cs:6165, read at 6721), and inflicted casualties per unit type are saved too. So the numbers themselves aren't lost. What's lost is the merge back into the brigade on reattach (BattleUnits.cs:12858–12864). Separately, `ReattachCompany` has a vanilla typo at 12863: it adds the brigade's own prisoner count to itself instead of the detachment's.

#### The fix
- **Resolver fallback** (prefix on `ImportExportUnitData.GetRegGameObjectFromPosition`):
  - It reads exactly the same four lines, so the save format is unchanged and old saves load.
  - It tries the vanilla name + 0.5-grid match first.
  - If that fails, it falls back to a unit with the same name: the only one if the name is unique, otherwise the nearest one within 30 world units.
  - `"Null"` stays null.
  - This applies to every unit reference in `battleunits.dat` and to `Artillery.priorparent`.
- **Re-link pass** (postfix on `LoadBattleUnitData`):
  1. If only one side of a pair resolved, it restores the other side (`parent.detachedregiment` ↔ `detachment.parentregiment`).
  2. If both are null, it takes each unit named exactly `GameVars.commander[c].name + "'s Detachment"` (unittyp 2–4) and links it to the nearest regiment of the same alliance with the same commander id (unittyp 0–2) that has no detachment and isn't a detachment itself.
  3. It re-parents every linked detachment to its brigade's transform parent, refreshes `UpdateAllParentUnits` on the old and new groups and `UpdateUnitSymbol`, and gives `currentcommand` back to the brigade if the detachment had taken it.
- **Prisoner merge** (prefix/postfix on `BattleUnits.ReattachCompany`): after a successful reattach, the brigade's `inflictedprisonersperunittype` is set to brigade-before + detachment-before.

#### Limitations and assumptions
- Not tested in game.
- The name fallback can pick the wrong unit if two units share a name and the saved position is far from both. The 30-unit radius guards against that, and a unique name is always accepted.
- Step 2 relies on the default detachment name. A detachment renamed by the player, or a commander renamed after detaching, is only recovered through steps 1 and 3.
- A commander leading two regiments that both have detachments could get them swapped. The nearest-distance rule makes that unlikely.
- `GameVars.import_commanders` runs after `LoadBattleUnitData` (ImportExportUnitData.cs:7642) and sets `currentcommand` to the last unit with that commander and no `parentregiment`. With the links restored, the detachment is now correctly excluded.

## Artillery features (src/ArtilleryFeatures.cs)

Switches: `Artillery.CapturedGunAmmo` (float, used only when PerGunAmmo is off), `Artillery.PerGunAmmo`, `Artillery.GunDestruction`, `Artillery.GunDestructionChance`.

### How vanilla works
- **Ammo is per regiment.** `Regiment.ammo[3]` holds fractions: 0 = solid shot (ball for infantry), 1 = shell, 2 = canister. It is set to 1 in `Initialize` (Regiment.cs 3894–3907).
  - Each artillery shot subtracts `1 / (standardroundsperunittyp × guns / generalcasualtiesadjustmentartillery / ammoconsumptionfactorart)` (Regiment.cs 10301–10306). So the fraction is already an "average per gun".
  - Resupply: `SetAmmoVariable` (8875). `LevelAmmo` averages ammo across a brigade, weighted by strength (BattleUnits.cs 8756–8812).
  - Firing never stops for lack of ammo. Rate of fire is only multiplied by the "ammo out" factor (0.05).
- **Gun models.** `Artillery` (Artillery.cs) holds only `weapon` and `condition`. `condition` is never set to anything but 0, and `Start()` resets it to 0 (Artillery.cs 42–48), which also wipes the value loaded from a save.
  - A battery's models are `Regiment.gunsprite` (private) and the count is `Regiment.guns`. Each model stands for about `artilleryspritespergun × gunspersprite` real guns (8 × 0.5 = 4 for foot artillery).
  - "Real" guns are `statsguns = ceil((sprites − 1) × gunspersprite)` (`UpdateStatsGuns`, Regiment.cs 26251–26276). They are derived from the crew. Every drop in statsguns is added to `gunslost`.
- **Leaving guns.** `LeaveGuns(howmany)` (26120) re-parents the models to the `LeftGuns` scene object. It is called on rout (14698), on a reattach order (20606, BattleUI.cs 4038), and when crew losses mean fewer models are needed (`RemoveUnitSprite`, 27934). `AdjustGunSprites` (26100) does the same after a save is loaded.
- **Capturing and recrewing.** `BattleUI.GrabLeftCannon` (BattleUI.cs ~5180–5243) does one of two things:
  - An artillery unit that is short of guns grabs them.
  - Otherwise, `BattleUnits.DetachCompany(inf, standardaridetachment, 2)` clones the infantry or cavalry regiment (BattleUnits.cs 12701). The clone keeps the infantry's `ammo` array: {rifle fraction, 1, 1}.

  Either way, `CheckForTakingGuns` (Regiment.cs 20526) later calls `GrabGuns` (26183), which never touches ammo. Both paths already skip guns with `condition != 0`. The AI's `CheckForLeftGuns` (AIBattle.cs 2946) does not check condition, but `GrabLeftCannon` filters again.
- **Gun losses after battle.** The losing side's captured weapons are `gunslost × U(rangefactorweaponscapturedfrom, …to)` (BattleUnits.cs 5913–5916; auto-resolve: Autocalc.cs 3936). These go to the winner's `weaponstock` (ImportExportUnitData.cs ~3419). The loser's stock is never reduced. A campaign battery's gun count is recalculated from crew strength.
- **Saves.** Left guns are saved to `artillery.dat`: weapon, transform, condition, priorparent (ImportExportUnitData.cs 7291–7362). Regiment `ammo`, `guns`, `gunslost` and `statsguns` are saved in the unit data.

### What the mod changes
1. **CapturedGunAmmo** (applies whenever PerGunAmmo is off; set it to 1 to get vanilla behaviour).
   - A postfix on `DetachCompany` runs when an infantry or cavalry unit detaches an artillery crew (targetunittype 2, which only happens from `GrabLeftCannon`). It sets the crew's ammo to {c, c, c}.
   - An existing battery that grabs guns keeps its own ammo, as in vanilla.
2. **PerGunAmmo**
   - Prefixes on `LeaveGuns` and `AdjustGunSprites` store the battery's current ammo on every model that leaves. The store is a `ConditionalWeakTable<Artillery, float[]>`.
   - A postfix on `GrabGuns` sets `ammo = (old × n_old + Σ stored) / (n_old + n_new)`, counted in gun models. Models with nothing stored use CapturedGunAmmo. The infantry's rifle ammo is ignored, because a fresh crew has n_old = 0.
   - A battery that loses guns to casualties keeps its fraction, since the fraction is per gun.
   - Brigade ammo levelling, resupply and the reattach merge are left as vanilla.
3. **GunDestruction**
   - A prefix on `Projectile.DestroyBullet` gives every solid-shot hit (ammotype 0, fired by artillery) on an enemy battery's block a `GunDestructionChance` chance to destroy one real gun.
   - The battery then gets an intact-gun cap: `Cap = statsguns − 1`. A postfix on `UpdateStatsGuns` clamps `statsguns` to the cap and adds the difference to `gunslost`, using vanilla's own bookkeeping. Spare crew can no longer bring a gun back.
   - When the cap needs fewer models, the model at the hit block (block / 3) is moved to the end of the list, set to `condition = 1`, and dropped with `LeaveGuns(1)`. If no guns remain, all models are marked and `LeaveGuns()` runs (the crew becomes "limbered", as on rout). Wrecks therefore stay on the field and can never be recrewed.
   - A `GrabGuns` prefix raises the cap by the guns grabbed. A crew that left its guns starts again from 0.
   - A Start() patch keeps a non-zero `condition`. A `UpdateLeftGuns` postfix removes wrecks from the AI's list.
   - A `SaveBattleResult` prefix hides destroyed guns from the winner's capture roll, and a finalizer restores them. They still count in `gunslost` and in the "guns lost" statistics.
4. **Battle save/load.** Both side tables live only in memory. The mod writes `bugfixmod_artillery.txt` next to `artillery.dat`:
   - Per-gun ammo is saved in the same `GetComponentsInChildren` order that vanilla uses, and read back after `LoadArtillery`.
   - Destroyed and cap counts per battery are saved by name and commander, and read back after `LoadBattleUnitData`.
   - With no sidecar (an older save, or the mod off at save time), left guns use CapturedGunAmmo and caps are lost. This is the least-bad fallback: you get vanilla behaviour, not an error.

### Difficulty, risks, untested assumptions
- **Nothing has been tested in game.** The code compiles against the decompiled DLL only.
- **Campaign gun counts are not reduced.** In the campaign, a battery's guns are a function of its men, so a battery that survives returns with full guns. Fixing that means killing crew or adding a persistent gun field. Both are invasive, so they are left out.
- **Capture roll.** The loser's own weapon stock is never reduced in vanilla. "Lost, not capturable" is therefore implemented only as exclusion from the winner's capture roll.
- **Model swap.** Swapping models inside `gunsprite` before `LeaveGuns(1)` assumes the layout is rebuilt from the list order. `SetRegimentSpritePos` is called straight after, so this should hold. Watch for a model briefly snapping to another block.
- **Sidecar matching.** It relies on left-gun hierarchy order and on unit names staying stable. If the gun count differs, a warning is logged.
- **Guns left while PerGunAmmo was off** carry no stored ammo, so they get CapturedGunAmmo when grabbed.
- **Detachments that merge back.** When a capturing detachment reattaches to its infantry, it is deleted with its `gunslost`. This is vanilla behaviour, and its destroyed-gun record goes with it.
- **Balance.** The default is 0.05 per block hit. At higher values concentrated counter-battery fire with solid shot will destroy guns quickly, because each bouncing ball can hit several blocks. Tune `GunDestructionChance` (0.03–0.05 is probably closer to historical).

## Extended battle report and DLC capture prestige (src/BattleReport.cs)

Switches: `Reporting.ExtendedBattleReport`, `Reporting.DlcCapturePrestige`, `PrestigePerOfficerRank`, `PrestigePerCapturedWeaponValue`.

### Where vanilla tracks this
- **Captured weapons** – an `int[weaponId]` array, built only for the loser's units:
  - Auto-resolve: `Autocalc.FinishLandBattle` (Autocalc.cs ~3912–3943): (losses − priorwounded) × U(`rangefactorweaponscapturedfrom`, `…to`) + captured men (+ whole garrison strength); artillery uses `gunslost` × the same factor.
  - Manual battle: `BattleUnits.SaveBattleResult` (BattleUnits.cs ~5895–5918), same formula.
  - Both pass it to `ImportExportUnitData.ExportBattleResultData` (ImportExportUnitData.cs 2406), which writes `Battlefields/999/A/battleresult.dat`.
  - `WriteBattleResultToMonument` (ImportExportUnitData.cs 2738) reads it back, stores it on the `BattleMonument`, and adds it to the winner's `weaponstock` (~3419).
  - The only player-facing use is the "Results of …" dispatch (message type 50, `Messages.cs` ~561–615), which collapses it to "N rifles and M guns". The newspaper only says "a small/large amount of weapons" (~3295).
- **Officer casualties** – `Commander.status`: 0 active, 2 captured (POW), 3 wounded, 4 killed, 5 DLC avatar out.
  - Set by `Regiment.UpdateCommanderCasualtyState` (Regiment.cs 17788–17869). A routed unit's commander can surrender (status 2). `Regiment.Surrender` sets status 2 (~14998).
  - Killed and wounded lists: `BattleUnits.GetKIACommanders(side, …, minimumhierarchyshowkiacommanders, status)` (BattleUnits.cs 6104), called for status 4 and 3 in both paths (BattleUnits.cs 6049–6052, Autocalc.cs 4095–4098). Only commanders who still have a `currentcommand` and are at least the configured hierarchy level (campaignprefs: 13 = brigade HQ) are listed.
  - Vanilla shows them in a separate "officer loss report" message (type 61, ImportExportUnitData.cs 2715 and 3242) for the player's side only, plus up to 5 names in the newspaper.
  - **Captured officers are never listed by vanilla.** Garrison surrenders in auto-resolve (`SurrenderUnitCampaign`, Autocalc.cs 4179) just delete the units, and their commanders keep status 0.
- **DLC prestige** – `DLC_WL.AdjustPrestige(amount, 1, descr)` (DLC_WL.cs 3087) applies `EffectMultiplier(0)` and the incident multipliers. Typical gains (dlcwl_config.dat): 0.125 per casualty inflicted, 10 per enemy unit routed, 100–150 for operation goals, 500 per year for defending the capital.

### What the patch adds
1. While `FinishLandBattle` or `SaveBattleResult` runs, it tracks commanders whose status goes 0 → 2 (patches on `UpdateCommanderCasualtyState` and `Surrender`). It also records the officers of garrison units handed to `SurrenderUnitCampaign`.
2. A postfix on `ExportBattleResultData` writes a sidecar file, `Battlefields/999/A/bugfixmod_report.txt`, holding: captured weapons by id, killed/wounded lists for both sides (the vanilla lists), captured and garrison officers (only participants still at status 2), and whether the DLC avatar's command took part (any unit with `dlcw_isundercommander`, or commanded by the avatar). A sidecar is needed because a manual battle reaches the campaign through a scene reload. Naval battles are skipped.
3. A prefix on `WriteBattleResultToMonument` reads the sidecar, grants the DLC prestige, and keeps the data for the message postfix. The sidecar is deleted afterwards.
4. A postfix on `Messages.Message.GenerateMessageContent` (type 50 only) appends the following to the "Results of …" dispatch:
   - "Weapons captured by our forces" (or "Weapons lost to the enemy"): amount × weapon name, guns first.
   - "Enemy officer casualties" and "Our officer casualties", each split into Killed / Wounded / Captured / Surrendered with the garrison, as full rank + name, at most 15 per list.
   - The prestige gained.

   The text is stored in the message, so it survives saves.

### Prestige formula (career DLC only, and only if the avatar's command took part)
- Weapons count only if the player's side won. For each captured weapon w:
  excess = vanilla weapon rating (`GetWeaponStrengthFactor` without the AiWeaponValue change) / own side's average vanilla rating over active units of the same kind (guns or the rest) − 1, counted only if > 0.
  points = Σ excess × amount × (100 if a gun, 1 otherwise).
  weapon prestige = min(points × 0.25, 1000) × `PrestigePerCapturedWeaponValue` (default 0.1, i.e. 10 %).
  Example: 2,000 rifles 30% better than average give 15.
- Officers: `PrestigePerOfficerRank` × rank (1 Lt … 9 Gen) × (0.1 + the officer's fame) for each enemy officer captured or surrendered with a garrison, half that for each enemy officer killed or wounded. Fame is the commander's raw 0–1 fame value, read directly because `GetCommanderFame()` is already cut for killed or captured commanders. Example: a captured brigadier general (rank 6) with fame 0.4 gives 10 × 6 × 0.5 = 30 at the default of 10; an unknown one (fame 0) gives 6.
- Paid through `DLC_WL.AdjustPrestige(total, 1, "captured weapons and officers")`, so the career multipliers apply. The report shows the amount actually gained.

### Limitations and untested assumptions
- Not tested in game. It compiles against the decompiled DLL only.
- Captured officers outside garrison surrenders only happen when a routed unit's commander rolls surrender. In auto-resolve that roll runs only for top-level units (`parentregiment == null`), so captures are rare.
- Killed/wounded lists use vanilla's filter, so only brigade-level and higher commanders who still hold a command are listed. Regiment colonels are not.
- Garrison officers are listed as "surrendered with the garrison" even though vanilla leaves their status at 0 (they are not made POWs).
- Assumes commander ids are the same in the battle scene and the reloaded campaign. Vanilla's own KIA lists rely on the same thing.
- If vanilla's `messages.Add` rejects the dispatch as a duplicate, the extra text is not shown, but the prestige is still granted.
- The rifle quality average is taken over all non-artillery units, including cavalry carbines, as vanilla does.
- The constants in the weapon formula (×100 per gun, ×0.25, cap 1000) are guesses. Use `PrestigePerCapturedWeaponValue` to tune.

## UI performance (src/UiPerformance.cs)

### Campaign OOB rebuilds 6–9 times per click – `Performance.CampaignOOB`
**Cause.** Opening the OOB tab (`CampaignMainPanel.SetTab` → `ChangeSelection(0)`) asks for a rebuild four times:
- `SwitchUnitView(0)` before the content update – `UpdateOOBs(overridelastvar: true)`
- `UpdateContent` – `UpdateOOBs()`
- `UpdateUnitData` – `UpdateOOBs()`
- `SwitchUnitView(0)` again after it – `UpdateOOBs(overridelastvar: true)`

`UpdateOOBs` (CampaignMainPanel.cs ~1467) rebuilds all three OOB windows. Two of them are the split-view windows, which are hidden unless you are in split view. So a normal open builds the OOB at least 6 times, and 9 when the selected army changes partway through (`SelectOOBUnitFromCampaign`).

Each build (`OOB.UpdateOOB`, OOB.cs ~224) is expensive for a big army:
- Every unit icon is a separate prefab with its own Canvas and CanvasScaler, instantiated fresh (`PanelHandler.AddMultiplePanel` → `new Panel(..., instantiatenew: true)`), plus a "+" prefab per open slot and a line object per link. The old ones are destroyed.
- Every icon runs `UpdateGroupStrength` twice: once in `AddUnitIcon`, once in `GetUnitTooltipInfo`. In the campaign that is a full stats pass over the unit's whole subtree. So the army HQ re-walks the whole army, each corps its corps, and so on.
- `AddMultiplePanel` looks for a free slot by scanning the panel list. Icons destroyed earlier in the same frame don't free their slots until the end of the frame, so each build appends instead of reusing.

**Fix.**
- Inside `SetTab`, `ChangeSelection` and `SwitchUnitView`, `UpdateOOB` only records which unit each window should show. When the outermost of those returns, each window is built once, with the last unit requested.
- The two split-view windows aren't built while their panels are hidden. They are marked stale, so they rebuild when split view is opened (which already forces a rebuild).
- Within one build, `UpdateGroupStrength` runs at most once per unit (separately for its two modes), so the tooltip reuses the numbers just computed for the icon.

Expected effect: about one sixth to one ninth of the previous work on open. Other OOB updates (drag-and-drop, recruiting, battle OOB) are unchanged apart from the strength memo.

**Not changed:** a build is still proportional to army size, because of the per-unit Canvas prefab. Fixing that would mean redesigning the icon prefab.

### Save/load menu builds its list twice – `Performance.SaveMenu`
**Cause.** The Save and Load buttons (PanelHandler.cs ~6210, ~6387, ~6443) call `Saving.InitializeFilters()` and then `UpdateCompleteList()`.
- `InitializeFilters(checkall: true)` ticks every filter entry through `CheckSelection`, which leaves all three filters flagged `updated`.
- On the next frame `Saving.Update` → `CheckFilterChange` (Saving.cs ~860) sees the flags, runs `InitializeFilters` again and rebuilds the whole list a second time.
- The list covers every save of every scenario and battle, not only the current campaign. For each save it:
  - opens `version.dat`, and `scenario.dat` twice
  - instantiates a row prefab
  - calls `Resources.LoadAll` for the faction flag
- `MainMenu.GetScenarioList` scans the scenario folders, twice per `InitializeFilters`.

**Fix.**
- After the menu-opening `InitializeFilters(checkall: true)`, the three `updated` flags are cleared, so the second rebuild doesn't happen. Filter clicks still set the flags and rebuild as before.
- Flag sprite arrays are cached by path.

Expected effect: roughly half the open time.

**Not changed:** the list still covers all scenarios, so a large save folder (many autosaves) is still slow. The cheapest relief is deleting old autosaves. A further patch could pre-select only the current campaign in the bottom filter, or cache each save's metadata by folder timestamp.

## Battle profiling (src/PerfLog.cs) – `Performance.PerfLog`

The game already times about 35 sections of its per-frame work (`Tools.UpdateCounterInMs` into `GameVars.debug_performancesingle`, always on), but nothing reads them: the debug overlay prints `debug_performance`, which is never filled, so it shows zeros. With `PerfLog = true` the mod writes the timers to LogOutput.log every `PerfLogSeconds` (default 15): average frame time and fps, unit count, game speed, total timed script work, and the 12 most expensive sections in ms per frame. Labels name the call that closes each timed section; slots 8–10 are shared with sound and sprite code. Rendering, physics and Unity overhead are not covered, so "frame time minus timed script work" is roughly everything else.

**Settings worth knowing (Config/debug.txt, no mod needed):**
- "Activate own scenelog.dat" is `true` in the AOM config: every `GameVars.DebugOwnLog` call (hundreds of call sites, many in battle AI) builds a timestamped string and writes it to scenelog.dat. Set it to `false` unless debugging. "Enable debug logs" is already `false`, which stops Unity's own log output.
- "Terrain detail draw distance" (150), "Terrain density" (0.08) and "Terrain pixel error battles" (4) control grass and terrain detail, a rendering cost.

## Reference: campaign disintegration and battle surrender (not changed by the mod)

### Campaign disintegration
- **Every land retreat** goes through `SetRetreatToDepot` (Regiment.cs:6846), which queues a `Retreat.RetreatOrder`. Retreats are triggered by:
  - low morale on contact (groupmorale < 0.275)
  - being too weak to fight (under 1,100 men and fewer than the enemy, or a strength ratio under 0.23)
  - losing a battle
  - transport interception
  - renewal: a retreat that ends on enemy ground, or with an enemy within bugle range, is re-issued with no limit (Regiment.cs:6681–6696).
- **Destination** (`GetRetreatDestinationLand`, Retreat.cs:477–627): the closest own town, fort, depot, small IIP or friendly unit within ±90° of "away from the nearest enemy". It must be 35–300 units away, with no enemy near it. If none qualifies, or if pathing finds no route (enemy units are switched on as obstacles, so encirclement counts), the unit disintegrates.
- **`DisintegrateUnit`** (Regiment.cs:6730). There is no warning before it, only a message after. Readiness goes to 0. Then:
  - **A friendly town exists (any distance):** each combat regiment loses 25–70 % as deserters (more when national morale is low), and the force is moved next to the town. The commander stays.
  - **No town:** `BattleUnits.DeleteUnit`. Only the top unit's own men and weapons are refunded to the pool and stock. Subordinate units are destroyed without any accounting, and the top commander is released (not captured).

### Battle surrender
- **Triggers:**
  - **Rout under bad national morale:** a routing unit may surrender when national morale ≤ 0.4 and army morale ≤ 0.5. The chance is 0.01 / (nat × avg)^1.5.
  - **Fully outflanked:** a routed unit fully outflanked in melee surrenders at once.
  - **Caught in melee while routed:** a 0.22 h timer runs while the routed unit is in melee, resetting after 0.044 h without melee. When it expires, the unit walks to the enemy with surrender sprites and surrenders.
  - **No retreat path:** a retreat order finds no path with an enemy adjacent.
- **`Surrender()`** (Regiment.cs:14957):
  - POW share = 0.3 ± 0.2, reduced by experience (up to ×0.8) and fighting spirit (up to ×0.6), booked as `losses[3]`.
  - The brigade commander becomes a POW, except for skirmishers.
  - The rest go back to the campaign as survivors.
- **Back in the campaign** (ImportExportUnitData.cs:3166–3194):
  - Prisoners = POW + MIA + a share of the wounded (loser 15–30 %, winner 0–7 %). They go to the enemy's nearest POW camp, or are paroled if the enemy has none.
  - Only the winner captures weapons: the POWs' weapons plus 30–60 % of other losses. Guns: 30–60 % of guns lost.
  - Captured commanders return at 0.6–0.86 a year, by rank.

### Looks wrong (unpatched; candidates for a later fix)
1. **Retreat fallback heads for the enemy.** `GetRetreatDestinationLand` overwrites `fromposition` with the enemy's position (Retreat.cs:529) and returns it when nothing qualifies (:626). The "no destination" test compares against the unit's original position, so it can pass, and the unit is ordered toward the enemy. Usually the path is blocked, and the unit disintegrates anyway.
2. **Deleting a corps or army loses everything below the HQ.** Men, weapons and subordinate commanders all vanish.
3. **Escape exploit.** The town fallback has no distance limit, so an encircled army is teleported to a far friendly town for 25–70 % losses.
4. **More POWs than the config ratio.** `SufferCasualtiesAll` applies the POW ratio once per block to the shrinking strength (Regiment.cs:17436), probably giving 1 − (1 − r)^n.
5. **AI army surrender check is inverted.** `CheckAISurrender` (BattleUnits.cs:5830) only surrenders while reinforcements *are* arriving, the opposite of the config label. It also never checks the "morale below enemy" condition.
6. Skirmishers and permanently detached units are skipped by the desertion share and are not moved with the force.
