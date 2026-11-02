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
