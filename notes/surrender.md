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
