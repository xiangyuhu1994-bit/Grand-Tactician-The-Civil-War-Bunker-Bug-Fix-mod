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
