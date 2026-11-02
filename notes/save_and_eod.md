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
