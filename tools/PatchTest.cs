// Headless patch test: applies every bugFixMod Harmony patch to the game's real Assembly-CSharp
// under Mono with the same HarmonyX/MonoMod that BepInEx 5.4.23 ships.
using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

static class Harness
{
    static int Main(string[] args)
    {
        var here = System.IO.Path.GetFullPath(".");
        var setExe = typeof(BepInEx.Paths).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).First(m => m.Name == "SetExecutablePath");
        var pars = setExe.GetParameters();
        var argv = new object[pars.Length];
        argv[0] = System.IO.Path.Combine(here, "Game.exe");
        for (int i = 1; i < argv.Length; i++) argv[i] = pars[i].ParameterType == typeof(string) ? here : (pars[i].HasDefaultValue ? pars[i].DefaultValue : null);
        setExe.Invoke(null, argv);
        var mod = Assembly.LoadFrom("bugFixMod.dll");
        var log = new ManualLogSource("bugFixMod");
        log.LogEvent += (s, e) => Console.WriteLine("[" + e.Level + "] " + e.Data);

        var plugin = mod.GetType("BugFixMod.BugFixPlugin");
        plugin.GetField("Log", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).SetValue(null, log);

        var cfgPath = System.IO.Path.GetFullPath("test.cfg");
        if (System.IO.File.Exists(cfgPath)) System.IO.File.Delete(cfgPath);
        var cfgFile = new ConfigFile(cfgPath, true);
        var cfg = mod.GetType("BugFixMod.Cfg");
        cfg.GetMethod("Bind", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { cfgFile });

        bool allOn = args.Contains("--all-on");
        if (allOn)
            foreach (var f in cfg.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (f.GetValue(null) is ConfigEntry<bool> b) b.Value = true;
        Console.WriteLine("Config: all bool switches " + (allOn ? "ON" : "at defaults"));

        var harmony = new Harmony("aom.bugfixmod.test");
        int ok = 0, failed = 0, skipped = 0;
        foreach (var type in AccessTools.GetTypesFromAssembly(mod))
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
            try
            {
                var patched = harmony.CreateClassProcessor(type).Patch();
                if (patched == null || patched.Count == 0) { skipped++; Console.WriteLine("SKIP " + type.Name + " (Prepare=false)"); }
                else { ok++; Console.WriteLine("OK   " + type.Name + " (" + patched.Count + " method(s))"); }
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("FAIL " + type.Name + ":\n" + e);
            }
        }
        Console.WriteLine("RESULT applied=" + ok + " skipped=" + skipped + " failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
