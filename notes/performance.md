## Battle profiling (src/PerfLog.cs) – `Performance.PerfLog`

The game already times about 35 sections of its per-frame work (`Tools.UpdateCounterInMs` into `GameVars.debug_performancesingle`, always on), but nothing reads them: the debug overlay prints `debug_performance`, which is never filled, so it shows zeros. With `PerfLog = true` the mod writes the timers to LogOutput.log every `PerfLogSeconds` (default 15): average frame time and fps, unit count, game speed, total timed script work, and the 12 most expensive sections in ms per frame. Labels name the call that closes each timed section; slots 8–10 are shared with sound and sprite code. Rendering, physics and Unity overhead are not covered, so "frame time minus timed script work" is roughly everything else.

**Settings worth knowing (Config/debug.txt, no mod needed):**
- "Activate own scenelog.dat" is `true` in the AOM config: every `GameVars.DebugOwnLog` call (hundreds of call sites, many in battle AI) builds a timestamped string and writes it to scenelog.dat. Set it to `false` unless debugging. "Enable debug logs" is already `false`, which stops Unity's own log output.
- "Terrain detail draw distance" (150), "Terrain density" (0.08) and "Terrain pixel error battles" (4) control grass and terrain detail, a rendering cost.
