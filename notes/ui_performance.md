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
