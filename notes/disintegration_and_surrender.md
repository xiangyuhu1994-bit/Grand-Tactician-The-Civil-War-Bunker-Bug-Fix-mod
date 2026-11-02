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
