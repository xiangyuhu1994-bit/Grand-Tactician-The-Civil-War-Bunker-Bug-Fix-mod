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
