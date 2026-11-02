// Trade and import pricing. Write-up: notes/economy.md

using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // Tariff markup leaks onto domestic trade.
    // IIP.UpdateEconomy (IIP.cs ~941) sets the tariff markup (1 + cap x rate) the first time it
    // meets a connection to another side's hub and never resets it to 1, so every later
    // connection in that hub's list - domestic ones included - is treated as an import when
    // deciding how attractive its goods are. No tariff revenue is collected on those, but
    // domestic flow out of hubs with a foreign link is dampened, depending on link order.
    // Fix: the markup is computed per connection.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(IIP), nameof(IIP.UpdateEconomy))]
    static class TariffMarkup_Patch
    {
        static bool Prepare() => Cfg.TariffPerConnection.Value;

        public static float Markup(IIP iip, int j)
        {
            var c = iip.connectediips[j];
            if (c == null || c.allianceowner == iip.allianceowner) return 1f;
            var al = GameVars.alliance[iip.allianceowner];
            return 1f + al.taxcaps[GameVars.tax_tarifs] * al.tax[GameVars.tax_tarifs];
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var envoyField = AccessTools.Field(typeof(GamePrefs), nameof(GamePrefs.project_sendenvoystradevolume));
            int n = 0;
            // Locals: trade-deal factor (stored right after project_sendenvoystradevolume), then the
            // markup (initialised to 1), then the loop index j.
            int e = code.FindIndex(ci => ci.LoadsField(envoyField));
            object dealLocal = null, markupLocal = null, jLocal = null;
            if (e >= 0)
            {
                int k = e;
                while (k < code.Count && !code[k].IsStloc()) k++;
                if (k < code.Count) dealLocal = code[k].operand;
                k++;
                while (k < code.Count && !code[k].IsStloc()) k++;
                if (k < code.Count && k > 0 && code[k - 1].opcode == OpCodes.Ldc_R4) markupLocal = code[k].operand;
                k++;
                while (k < code.Count && !code[k].IsStloc()) k++;
                if (k < code.Count) jLocal = code[k].operand;
            }
            if (markupLocal != null && jLocal != null && dealLocal != null)
                for (int i = 0; i + 3 < code.Count; i++)
                {
                    // ... relprice[k] * markup / dealfactor ...
                    if (code[i].IsLdloc() && Same(code[i].operand, markupLocal) && code[i + 1].opcode == OpCodes.Mul
                        && code[i + 2].IsLdloc() && Same(code[i + 2].operand, dealLocal) && code[i + 3].opcode == OpCodes.Div)
                    {
                        code[i].opcode = OpCodes.Ldarg_0;   // in place: keeps any labels
                        code[i].operand = null;
                        code.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, jLocal));
                        code.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TariffMarkup_Patch), nameof(Markup))));
                        n++;
                    }
                }
            BugFixPlugin.Log.LogInfo("Tariff markup per connection: " + n + " site(s)");
            return code;
        }

        static bool Same(object a, object b) =>
            a == b || (a is LocalBuilder la && b is LocalBuilder lb && la.LocalIndex == lb.LocalIndex);
    }

    // ------------------------------------------------------------------------------
    // Union weapon imports priced by Europe's sympathy for the Confederacy.
    // Weapon.GetProductionFigures (GameVars.cs ~5280) sets the relations term for imports to
    // (P(Britain) + P(France) - P(Spain)) / 3 in both branches of an alliance check, where
    // P are intervention probabilities on the Confederate side for Britain/France and on the
    // Union side for the Spain. Both sides get the Confederate number, so Europe warming
    // to the Confederacy made Union imports cheaper too. Fix: the Union gets the mirror image -
    // Britain's and France's lean toward the Union minus the Spain's lean toward the
    // Confederacy, scaled by the same chapter threshold. The Confederacy is unchanged.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(GameVars.Weapon), nameof(GameVars.Weapon.GetProductionFigures))]
    static class ImportRelations_Patch
    {
        static bool Prepare() => Cfg.ImportPriceRelationsPerSide.Value;

        public static float Relation(int alliance)
        {
            float v;
            if (alliance == 0) v = (Lean(0, 2) + Lean(0, 3) - Lean(1, 4)) / 3f;
            else v = (GameVars.Alliance.GetInterventionProbability(2) + GameVars.Alliance.GetInterventionProbability(3)
                      - GameVars.Alliance.GetInterventionProbability(4)) / 3f;
            return Mathf.Clamp01(v);
        }

        // How far European power k leans toward side `toward`, on the same scale as
        // GetInterventionProbability (chapter threshold, Britain's higher bar).
        static float Lean(int toward, int k)
        {
            if (GameVars.alliance == null || GameVars.alliance.Length <= k || Policy.CurrentChapter < 0) return 0f;
            var me = GameVars.alliance[toward].interventionlevels;
            var them = GameVars.alliance[1 - toward].interventionlevels;
            if (me == null || them == null || me.Length <= k || them.Length <= k) return 0f;
            float d = me[k] - them[k];
            if (k == 2) d /= Mathf.Max(1f, GamePrefs.higherinterventionlevels);
            var thr = GamePrefs.relationsintervention;
            if (thr == null || Policy.CurrentChapter >= thr.Length || thr[Policy.CurrentChapter] >= 100f) return 0f;
            return Mathf.Clamp01(d / Mathf.Max(0.0001f, thr[Policy.CurrentChapter]));
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var rel = AccessTools.Field(typeof(GamePrefs), nameof(GamePrefs.weaponcostfactorrelations));
            int n = 0;
            for (int i = 1; i < code.Count; i++)
            {
                if (!code[i].LoadsField(rel) || !code[i - 1].IsLdloc()) continue;
                code[i - 1].opcode = OpCodes.Ldarg_1;   // _alliance; in place keeps labels
                code[i - 1].operand = null;
                code.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ImportRelations_Patch), nameof(Relation))));
                n++;
                break;
            }
            BugFixPlugin.Log.LogInfo("Import price relations per side: " + n + " site(s)");
            return code;
        }
    }
}
