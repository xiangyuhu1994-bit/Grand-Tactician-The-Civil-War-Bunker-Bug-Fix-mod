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
