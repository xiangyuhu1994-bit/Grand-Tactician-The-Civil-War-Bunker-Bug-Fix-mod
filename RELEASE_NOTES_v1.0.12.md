First public release of bugFixMod, a BepInEx plugin that fixes dozens of campaign, battle, auto-resolve and economy bugs in Grand Tactician: The Civil War, found by reading the game's code. Highlights:

- **Campaign:** empty HQs no longer start phantom sieges; AI weapon purchases are sane; deserters are no longer handed to the enemy as prisoners; paroled and freed prisoners rejoin their side's manpower instead of removing it; the career player can become commander-in-chief.
- **Battle:** multi-day battle freeze, broken battle saves, artillery firing through friendly troops, skirmisher jitter, units ignoring orders near hidden enemies, and more.
- **Auto-resolve:** weapon quality (rate of fire, range, accuracy) now counts, so muskets no longer beat everything.
- Optional extras: per-gun artillery ammo, gun destruction, extended battle reports, a battle profiler.

Every fix has its own switch in `BepInEx/config/aom.bugfixmod.cfg`. The README explains each bug and fix in detail.

**Install:** install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (win_x64 5.4.x) into the game folder and run the game once. Then copy `BepInEx/plugins/bugFixMod.dll` from the attached zip into the game's `BepInEx/plugins/` folder. Check `BepInEx/LogOutput.log` for the "bugFixMod … loaded" line.

**Status:** every patch applies cleanly in game, but not every fix has been tested in play yet. Back up your saves, and please report problems with your `LogOutput.log` attached.
