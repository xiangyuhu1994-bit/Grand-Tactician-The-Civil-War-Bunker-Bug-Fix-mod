// Men leaving the army: deserters, expired contracts. Write-up: notes/manpower.md

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class Manpower
    {
        // Return n men to civilian life in `state`: undo their "recruited" count, which gives
        // the state its workforce back and makes them recruitable again (recruit pool =
        // potential - recruited). Never takes a counter below 0.
        public static void ToCivilian(int state, int alliance, int recruitingtype, int n)
        {
            if (n <= 0 || alliance < 0 || alliance > 1 || GameVars.nation == null || state < 0 || state >= GameVars.nation.Length) return;
            var nat = GameVars.nation[state];
            int[] counter = recruitingtype == 1 ? nat.draftsrecruited : nat.volunteersrecruited;
            if (counter == null || alliance >= counter.Length) return;
            int take = Mathf.Min(n, Mathf.Max(0, counter[alliance]));
            if (take > 0) nat.AdjustRecruitingPool(-take, recruitingtype == 1 ? 1 : 0, alliance);
        }

        // Deserters who will be returned to the army (arrests, amnesties), per [state, alliance].
        public static float[,] Absent;

        public static void EnsureAbsent()
        {
            int n = GameVars.nation != null ? GameVars.nation.Length : 0;
            if (Absent == null || Absent.GetLength(0) != n) Absent = new float[n, 2];
        }

        public const string File_ = "bugfixmod_absent.txt";
    }

    // ------------------------------------------------------------------------------
    // Deserters, and men whose contracts run out, are gone for good.
    // UpdateCampaignUnitStats (Regiment.cs ~6245) moves deserters from strength to losses[2];
    // CheckContractExpiries (~4600) drops the men who don't re-enlist. Neither touches the state's
    // "recruited" counters, so these men never rejoin the workforce or the recruit pool, and no
    // deserter ever comes back. Fix:
    // - Men leaving at contract expiry go home: their state gets them back as workforce and
    //   potential recruits.
    // - Of each night's deserters, DeserterReturnShare (default 0.35) are later returned to the
    //   army. Historically the Union's Provost Marshal General's bureau arrested 76,526 of roughly
    //   200,000 recorded deserters, and many more came back under the 1863-65 amnesties on both
    //   sides. They wait in a per-state pool and come back at DeserterReturnRate per year
    //   (default 3: on average four months away), filling under-strength units from their state;
    //   if none has room they go home instead. The rest go home at once.
    // The pool is saved alongside each campaign save (bugfixmod_absent.txt).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.UpdateCampaignUnitStats))]
    static class DeserterHandling_Patch
    {
        static void Prefix(Regiment __instance, out int __state) => __state = __instance.losses != null ? __instance.losses[2] : 0;

        static void Postfix(Regiment __instance, Regiment groupunit, int __state)
        {
            var r = __instance;
            if (r.losses == null) return;
            bool inBattle = r.inbattle || (groupunit != null && groupunit.inbattle);
            if (!inBattle) DesertersNotPrisoners_Patch.Baseline[r] = r.losses[2];

            int deserted = r.losses[2] - __state;
            if (deserted <= 0 || r.unittyp > 13) return;
            if (Cfg.DeserterStatistic.Value && r.alliance == GameVars.playeralliance && GameVars.nation != null && r.state >= 0 && r.state < GameVars.nation.Length)
                GameVars.nation[r.state].deserters += deserted;
            if (!Cfg.DesertersGoHome.Value || r.alliance > 1) return;
            int back = Mathf.RoundToInt(deserted * Mathf.Clamp01(Cfg.DeserterReturnShare.Value));
            Manpower.ToCivilian(r.state, r.alliance, r.recruitingtype, deserted - back);
            if (back > 0 && r.state >= 0 && GameVars.nation != null && r.state < GameVars.nation.Length)
            {
                Manpower.EnsureAbsent();
                Manpower.Absent[r.state, r.alliance] += back;
            }
        }
    }

    [HarmonyPatch(typeof(Regiment), "CheckContractExpiries")]
    static class ContractExpiryHome_Patch
    {
        static void Prefix(Regiment __instance, out Dictionary<Regiment, int> __state)
        {
            __state = null;
            if (!Cfg.DesertersGoHome.Value || __instance.allattachedunits == null) return;
            __state = new Dictionary<Regiment, int>();
            foreach (var u in __instance.allattachedunits)
                if (u != null && u.losses != null) __state[u] = u.strength + u.losses[0];
        }

        static void Postfix(Dictionary<Regiment, int> __state)
        {
            if (__state == null) return;
            foreach (var kv in __state)
            {
                var u = kv.Key;
                if (u == null || u.losses == null) continue;
                int left = kv.Value - (u.strength + u.losses[0]);
                if (left > 0) Manpower.ToCivilian(u.state, u.alliance, u.recruitingtype, left);
            }
        }
    }

    // Returning deserters, on the periodic campaign data update.
    [HarmonyPatch(typeof(GameVars), nameof(GameVars.UpdateWorkforceFluctuation))]
    static class DeserterReturn_Patch
    {
        static float last;

        static void Postfix()
        {
            if (!Cfg.DesertersGoHome.Value || Manpower.Absent == null) return;
            float now = GameVars.GetCampaignFloatingDate();
            if (last <= 0f || now < last) { last = now; return; }
            float dt = Mathf.Min(now - last, 0.25f);
            last = now;
            if (dt <= 0f) return;
            float share = 1f - Mathf.Exp(-Mathf.Max(0f, Cfg.DeserterReturnRate.Value) * dt);
            Manpower.EnsureAbsent();
            for (int s = 0; s < Manpower.Absent.GetLength(0); s++)
                for (int a = 0; a < 2; a++)
                {
                    float pool = Manpower.Absent[s, a];
                    if (pool < 1f) continue;
                    int n = Mathf.Min((int)pool, Mathf.CeilToInt(pool * share));
                    if (n <= 0) continue;
                    Manpower.Absent[s, a] -= n;
                    int rest = Rejoin(s, a, n);
                    if (rest > 0) Manpower.ToCivilian(s, a, 0, rest);
                }
        }

        // Fill under-strength infantry/cavalry units of this state and side. Returns men left over.
        static int Rejoin(int state, int alliance, int n)
        {
            var list = GameVars.alliance[alliance].unitlist;
            if (list == null) return n;
            foreach (var u in list)
            {
                if (n <= 0) break;
                if (u == null || u.state != state || (u.unittyp != 0 && u.unittyp != 1) || u.losses == null) continue;
                if (!u.gameObject.activeInHierarchy || u.inbattle) continue;
                int max = BattleUnits.GetMaxUnitSize(alliance, u.unittyp);   // the game's own reinforcement cap
                int room = max - (u.strength + u.losses[0] + u.sick);
                if (room <= 0) continue;
                int add = Mathf.Min(room, n);
                u.strength += add;
                n -= add;
                if (u.weapon >= 0 && u.weapon < GameVars.alliance[alliance].weaponstock.Length)
                {
                    var stock = GameVars.alliance[alliance].weaponstock;
                    stock[u.weapon] = Mathf.Max(0f, stock[u.weapon] - add);   // re-armed from stock where possible
                }
            }
            return n;
        }
    }

    // Persist the absent pool with campaign saves.
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.SaveAdditionalUnitData))]
    static class AbsentSave_Patch
    {
        static void Postfix(string folder)
        {
            if (Manpower.Absent == null || string.IsNullOrEmpty(folder)) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                for (int s = 0; s < Manpower.Absent.GetLength(0); s++)
                    sb.Append(Manpower.Absent[s, 0].ToString(CultureInfo.InvariantCulture)).Append(';')
                      .Append(Manpower.Absent[s, 1].ToString(CultureInfo.InvariantCulture)).Append('\n');
                File.WriteAllText(Path.Combine(folder, Manpower.File_), sb.ToString());
            }
            catch (System.Exception e) { BugFixPlugin.Log.LogWarning("Saving deserter pool: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.LoadAdditionalUnitData))]
    static class AbsentLoad_Patch
    {
        static void Postfix(string folder, int mode)
        {
            if (mode != 0 || string.IsNullOrEmpty(folder)) return;
            Manpower.Absent = null;
            DesertersNotPrisoners_Patch.Baseline.Clear();
            try
            {
                string p = Path.Combine(folder, Manpower.File_);
                if (!File.Exists(p)) return;
                Manpower.EnsureAbsent();
                var lines = File.ReadAllLines(p);
                for (int s = 0; s < lines.Length && s < Manpower.Absent.GetLength(0); s++)
                {
                    var f = lines[s].Split(';');
                    if (f.Length < 2) continue;
                    float.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out Manpower.Absent[s, 0]);
                    float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out Manpower.Absent[s, 1]);
                }
            }
            catch (System.Exception e) { BugFixPlugin.Log.LogWarning("Loading deserter pool: " + e.Message); }
        }
    }

    // ------------------------------------------------------------------------------
    // The per-state "Deserters" figure (recruiting list) is badly inflated.
    // Nation.deserters only grows at the end of a battle: Autocalc.FinishLandBattle (~4058) and
    // BattleUnits.SaveBattleResult (~5964) add each of the player's units' whole losses[2] -
    // its running desertion tally, which vanilla also keeps restoring - so every battle adds
    // all of a unit's past deserters again, while desertions between battles are never counted
    // as they happen. Fix: those battle-end additions are undone, and deserters are counted when
    // they desert (player's units, as in vanilla). Figures already in a save stay inflated.
    // ------------------------------------------------------------------------------
    [HarmonyPatch]
    static class DeserterStatistic_Patch
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Autocalc), "FinishLandBattle");
            yield return AccessTools.Method(typeof(BattleUnits), nameof(BattleUnits.SaveBattleResult));
        }

        static void Prefix(out int[] __state)
        {
            __state = null;
            if (!Cfg.DeserterStatistic.Value || GameVars.nation == null) return;
            __state = new int[GameVars.nation.Length];
            for (int i = 0; i < __state.Length; i++) __state[i] = GameVars.nation[i].deserters;
        }

        static void Finalizer(int[] __state)
        {
            if (__state == null || GameVars.nation == null) return;
            for (int i = 0; i < __state.Length && i < GameVars.nation.Length; i++) GameVars.nation[i].deserters = __state[i];
        }
    }

    // ------------------------------------------------------------------------------
    // Deserters handed to the enemy as prisoners, again and again.
    // losses[2] holds both campaign desertions and men missing in battle. After each battle
    // WriteBattleResultToMonument (ImportExportUnitData.cs ~3175) sends a unit's whole losses[2]
    // to the enemy's POW camps and zeroes it - and UpdateGroupStrength (Regiment.cs ~11388) then
    // restores it from the smoothed average missingavg, so the same deserters are handed over
    // again after the next battle. Fix: around the battle result each unit's losses[2] is cut to
    // what it gained since its last campaign update outside battle (the battle's own missing),
    // and afterwards the desertion tally is put back. Tactical battles only report wounded and
    // captured back to the campaign, so for them losses[2] is all desertions.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.WriteBattleResultToMonument))]
    static class DesertersNotPrisoners_Patch
    {
        public static readonly Dictionary<Regiment, int> Baseline = new Dictionary<Regiment, int>();

        static void Prefix(out Dictionary<Regiment, int> __state)
        {
            __state = null;
            if (!Cfg.DesertersNotPrisoners.Value || BattleUnits.completeunitlist == null) return;
            __state = new Dictionary<Regiment, int>();
            foreach (var r in BattleUnits.completeunitlist)
            {
                if (r == null || r.losses == null || r.unittyp > 13) continue;
                // No baseline (e.g. just after loading): treat the whole tally as desertions. Tactical
                // battles never write battle missing into losses[2] anyway (only [0] and [3]).
                int b = Baseline.TryGetValue(r, out int v) ? v : r.losses[2];
                b = Mathf.Clamp(b, 0, r.losses[2]);
                if (b <= 0) continue;
                r.losses[2] -= b;
                __state[r] = b;
            }
        }

        static void Finalizer(Dictionary<Regiment, int> __state)
        {
            if (__state == null) return;
            foreach (var kv in __state)
                if (kv.Key != null && kv.Key.losses != null) kv.Key.losses[2] += kv.Value;
            // Drop destroyed units.
            var dead = new List<Regiment>();
            foreach (var r in Baseline.Keys) if (r == null) dead.Add(r);
            foreach (var r in dead) Baseline.Remove(r);
        }
    }
}
