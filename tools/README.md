# Headless patch test

Applies every bugFixMod patch to the real game assembly under Mono, without starting the game.

1. Make a folder containing: the game's `Managed/*.dll` except `mscorlib.dll`, `System*.dll`, `Mono.*`, `netstandard.dll`; every DLL from `BepInEx/core`; and `bugFixMod.dll`.
2. `mcs -out:PatchTest.exe -r:0Harmony.dll -r:BepInEx.dll -r:System.Core.dll PatchTest.cs` (run in that folder).
3. `mono PatchTest.exe` (defaults) or `mono PatchTest.exe --all-on` (every switch on).

Each patch class prints OK or FAIL with the full exception, and the last line sums them up. "cant resolve internal call" warnings are expected – those are Unity engine calls with no engine present.

Limit: this uses stock Mono, not Unity's Mono fork, so a runtime quirk specific to Unity can still slip through.
