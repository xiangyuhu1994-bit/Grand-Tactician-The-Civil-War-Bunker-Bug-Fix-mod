// Auto-resolve (Autocalc) fixes. Write-up: notes/autoresolve.md
//
// 1. Sharpshooter and Zouave perks are swapped in auto-resolve. In the battle code perk 1 is
//    Sharpshooters (range/accuracy) and perk 2 is Zouaves (fatigue, running, no fatigue morale
//    penalty). Autocalc checks perk 2 for the sharpshooter firepower bonus, perk 1 for the
//    Zouave fatigue factor, and perk 1 for the Zouave fatigue-morale exemption.
// 2. Entrenchment always uses side 1's engineering points, whichever side is defending.
// 3. Counter-battery chance is looked up by side index instead of by the sides' stances.
// 4. Random picks use Random.Range(0, Count - 1); the int overload excludes the upper bound,
//    so the last unit/vessel in a list is never picked (and with two units, only the first).

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class AutoresolveHelpers
    {
        public static readonly MethodInfo HasPerk = AccessTools.Method(typeof(Regiment), nameof(Regiment.HasPerk));
        public static readonly MethodInfo GetPerkLevel = AccessTools.Method(typeof(Regiment), nameof(Regiment.GetPerkLevel));
        public static readonly FieldInfo EngineeringPoints = AccessTools.Field(typeof(Autocalc), nameof(Autocalc.engineeringpoints));
        public static readonly FieldInfo CbStancePairs = AccessTools.Field(typeof(GamePrefs), nameof(GamePrefs.cbstancepairs));
        public static readonly MethodInfo IntRange = AccessTools.Method(typeof(Random), nameof(Random.Range), new[] { typeof(int), typeof(int) });

        // Swap perk ids 1 <-> 2 on the constant pushed right before HasPerk/GetPerkLevel.
        public static IEnumerable<CodeInstruction> SwapPerks(IEnumerable<CodeInstruction> instructions, string where)
        {
            var code = new List<CodeInstruction>(instructions);
            int swapped = 0;
            for (int i = 0; i < code.Count - 1; i++)
            {
                var next = code[i + 1];
                if (!(next.Calls(HasPerk) || next.Calls(GetPerkLevel))) continue;
                if (code[i].opcode == OpCodes.Ldc_I4_1) { code[i].opcode = OpCodes.Ldc_I4_2; swapped++; }
                else if (code[i].opcode == OpCodes.Ldc_I4_2) { code[i].opcode = OpCodes.Ldc_I4_1; swapped++; }
            }
            BugFixPlugin.Log.LogInfo("Autoresolve perk ids swapped in " + where + ": " + swapped);
            return code;
        }

        public static float DefenderEngineeringPoints(float[] points, Regiment unit1, Autocalc ac)
        {
            int side = (ac.alliance != null && ac.alliance.Length > 1 && unit1 != null && unit1.alliance == ac.alliance[1]) ? 1 : 0;
            return points[side];
        }

        public static float CounterBatteryChance(float[,] pairs, int side, int enemySide, Autocalc ac)
        {
            var st = ac.usedstance;
            if (st == null || st.Length < 2) return pairs[side, enemySide];
            return pairs[Mathf.Clamp(st[side], 0, 3), Mathf.Clamp(st[enemySide], 0, 3)];
        }

        public static int RangeInclusiveTop(int min, int maxMinusOne) => Random.Range(min, maxMinusOne + 1);
    }

    [HarmonyPatch(typeof(Autocalc), "FightUnit")]
    static class Autoresolve_FightUnit_Patch
    {
        static bool Prepare() => Cfg.AutoresolveFixes.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(AutoresolveHelpers.SwapPerks(instructions, "FightUnit"));
            // engineeringpoints[1]  ->  DefenderEngineeringPoints(engineeringpoints, unit1, this)
            int fixedEntrench = 0;
            for (int i = 0; i < code.Count - 2; i++)
            {
                if (code[i].LoadsField(AutoresolveHelpers.EngineeringPoints) && code[i + 1].opcode == OpCodes.Ldc_I4_1
                    && code[i + 2].opcode == OpCodes.Ldelem_R4)
                {
                    code[i + 1] = new CodeInstruction(OpCodes.Ldarg_2);   // unit1 (arg 0 is this)
                    code[i + 2] = new CodeInstruction(OpCodes.Ldarg_0);
                    code.Insert(i + 3, new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(AutoresolveHelpers), nameof(AutoresolveHelpers.DefenderEngineeringPoints))));
                    fixedEntrench++;
                }
            }
            BugFixPlugin.Log.LogInfo("Autoresolve entrenchment index fixes: " + fixedEntrench);
            return code;
        }
    }

    [HarmonyPatch(typeof(Autocalc), "UpdateFatigue")]
    static class Autoresolve_Fatigue_Patch
    {
        static bool Prepare() => Cfg.AutoresolveFixes.Value;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            AutoresolveHelpers.SwapPerks(instructions, "UpdateFatigue");
    }

    [HarmonyPatch(typeof(Autocalc), "DoArtilleryAction")]
    static class Autoresolve_CounterBattery_Patch
    {
        static bool Prepare() => Cfg.AutoresolveFixes.Value;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.Method(typeof(float[,]), "Get");
            var code = new List<CodeInstruction>(instructions);
            bool afterPairs = false;
            int n = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].LoadsField(AutoresolveHelpers.CbStancePairs)) afterPairs = true;
                if (afterPairs && code[i].opcode == OpCodes.Call && code[i].operand is MethodInfo m && m.Name == "Get"
                    && m.DeclaringType == typeof(float[,]))
                {
                    code.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                    code[i + 1] = new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(AutoresolveHelpers), nameof(AutoresolveHelpers.CounterBatteryChance)));
                    afterPairs = false;
                    n++;
                }
            }
            BugFixPlugin.Log.LogInfo("Autoresolve counter-battery index fixes: " + n);
            return code;
        }
    }

    [HarmonyPatch]
    static class Autoresolve_RandomPick_Patch
    {
        static bool Prepare() => Cfg.AutoresolveFixes.Value;

        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "PickUnit", "PickTargetVessel", "PickRandomUnit", "CalculateArtilleryOverruns", "DoArtilleryAction" })
            {
                var m = AccessTools.Method(typeof(Autocalc), name);
                if (m != null) yield return m;
            }
        }

        // Only rewrite Random.Range(int, int) calls whose upper bound is "... - 1".
        // No Label/MethodBase use here: on the game's runtime that made the transpiler fail to JIT
        // (TypeLoadException). The call is swapped in place, which keeps its labels.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int n = 0;
            for (int i = 2; i < code.Count; i++)
            {
                if (code[i].Calls(AutoresolveHelpers.IntRange) && code[i - 1].opcode == OpCodes.Sub && code[i - 2].opcode == OpCodes.Ldc_I4_1)
                {
                    code[i].opcode = OpCodes.Call;
                    code[i].operand = AccessTools.Method(typeof(AutoresolveHelpers), nameof(AutoresolveHelpers.RangeInclusiveTop));
                    n++;
                }
            }
            BugFixPlugin.Log.LogInfo("Autoresolve random-pick fixes: " + n);
            return code;
        }
    }
}

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // Auto-resolve weapon quality.
    // FightUnit's weapon term for infantry/cavalry is sqrt(firepower[0]) and nothing else
    // (Autocalc.cs ~3157). Rate of fire, range and accuracy are ignored, so smoothbore muskets
    // (FP 1.7) beat rifle-muskets (FP 1.0) and repeaters (FP 0.6) come out worst.
    // Fix: multiply that term by sqrt(ROF factor × accuracy factor × range factor), each
    // normalised so a Springfield M1855/61-class rifle-musket (ROF 3, 350 yd) is 1. Artillery
    // keeps vanilla's per-distance firepower: machine-gun ROF values (40–200) aren't on a
    // comparable scale.
    // Also: both branches test `weapon > 0`, so a unit armed with weapon 0 (the M1841 6-pounder)
    // gets a firepower term of 0 and inflicts no casualties at all. Changed to `weapon >= 0`.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Autocalc), "FightUnit")]
    static class Autoresolve_WeaponQuality_Patch
    {
        static bool Prepare() => Cfg.AutoresolveWeaponQuality.Value;

        const float RefRof = 3f, RefRange = 350f, RefRawAccuracy = 0.9f;
        static readonly float[] RangeWeightByDistance = { 1f, 0.5f, 0f };   // defend, attack, assault

        public static float SmallArmsFactor(float sqrtFirepower, Regiment unit, Autocalc ac)
        {
            try
            {
                if (unit == null || unit.weapon < 0 || unit.weapon >= GameVars.weapon.Length) return sqrtFirepower;
                var w = GameVars.weapon[unit.weapon];

                float rof = Mathf.Pow(Mathf.Max(0.1f, w.roundsperminute_pract) / RefRof, Cfg.AutoresolveRofExponent.Value);

                float acc;
                if (Cfg.AutoresolveAccuracyMatchesBattle.Value)
                {
                    // Same blend and clamp as Regiment.FireBullet / Projectile.SetTargetNoise.
                    float ew = GamePrefs.experienceinfluenceonaccuracy, mw = GamePrefs.moraleinfluenceonaccuracy;
                    float a = (w.accuracy + ew * unit.Experience() + mw * unit.morale) / (1f + ew + mw);
                    a -= a * GamePrefs.fatigueinfluenceonaccuracy * unit.fatigue;
                    acc = Mathf.Max(a, 1f);
                }
                else
                {
                    acc = Mathf.Max(0.1f, w.accuracy) / RefRawAccuracy;
                }
                acc = Mathf.Pow(acc, Cfg.AutoresolveAccuracyExponent.Value);

                int d = Mathf.Clamp(ac.fightingdistance, 0, 2);
                float range = Mathf.Pow(Mathf.Max(10f, w.range_yd) / RefRange, Cfg.AutoresolveRangeExponent.Value * RangeWeightByDistance[d]);

                return sqrtFirepower * Mathf.Sqrt(rof * acc * range);
            }
            catch (System.Exception e)
            {
                BugFixPlugin.Log.LogWarning("Autoresolve weapon quality: " + e.Message);
                return sqrtFirepower;
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var weaponField = AccessTools.Field(typeof(Regiment), nameof(Regiment.weapon));
            var pow = AccessTools.Method(typeof(Mathf), nameof(Mathf.Pow));
            var code = new List<CodeInstruction>(instructions);
            int zeroFixes = 0, factorAdded = 0;

            // `ldarg.1; ldfld weapon; ldc.i4.0; ble` (weapon > 0 test) -> blt (weapon >= 0), first two only.
            for (int i = 1; i < code.Count - 2 && zeroFixes < 2; i++)
            {
                if (code[i - 1].opcode == OpCodes.Ldarg_1 && code[i].LoadsField(weaponField) && code[i + 1].opcode == OpCodes.Ldc_I4_0
                    && (code[i + 2].opcode == OpCodes.Ble_S || code[i + 2].opcode == OpCodes.Ble))
                {
                    code[i + 2].opcode = code[i + 2].opcode == OpCodes.Ble_S ? OpCodes.Blt_S : OpCodes.Blt;
                    zeroFixes++;
                }
            }

            // First Mathf.Pow after the first firepower load is the small-arms sqrt(firepower[0]).
            var firepower = AccessTools.Field(typeof(GameVars.Weapon), nameof(GameVars.Weapon.firepower));
            int fp = code.FindIndex(c => c.LoadsField(firepower));
            if (fp >= 0)
            {
                int p = code.FindIndex(fp, c => c.Calls(pow));
                if (p >= 0)
                {
                    code.InsertRange(p + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Autoresolve_WeaponQuality_Patch), nameof(SmallArmsFactor))),
                    });
                    factorAdded++;
                }
            }
            BugFixPlugin.Log.LogInfo("Autoresolve weapon quality: weapon-0 fixes " + zeroFixes + ", small-arms factor " + factorAdded);
            return code;
        }
    }
}
