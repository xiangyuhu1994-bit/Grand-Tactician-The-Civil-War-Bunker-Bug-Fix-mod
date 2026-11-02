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
