// bugFixMod – extended campaign battle report and career-DLC capture prestige.
//
// Flow (same for auto-resolved and manually fought land battles):
//  1. While a battle is being finished (Autocalc.FinishLandBattle or BattleUnits.SaveBattleResult),
//     officers whose status changes 0 -> 2 (captured) are tracked, and auto-resolve garrison
//     surrenders record the garrison's officers.
//  2. ImportExportUnitData.ExportBattleResultData (common to both paths) gets captured weapons and
//     killed/wounded officer lists; a postfix writes them, plus the captured officers, to a sidecar
//     file next to battleresult.dat (a manual battle returns to the campaign through a scene reload).
//  3. ImportExportUnitData.WriteBattleResultToMonument (campaign side) loads the sidecar, applies the
//     DLC prestige through DLC_WL.AdjustPrestige (so EffectMultiplier and incident multipliers apply),
//     and the "Results of ..." dispatch (message type 50) gets the extra text appended.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    internal static class BattleReport
    {
        const string SidecarPath = "Battlefields/999/A/bugfixmod_report.txt";
        const int MaxListed = 15;
        const float GunWeight = 100f;          // one captured gun counts like 100 rifles
        const float WeaponPrestigeScale = 0.25f;
        const float FameBase = 0.1f;
        const float WeaponPrestigeCap = 1000f; // per battle, before Cfg multiplier

        internal static bool Enabled => Cfg.ExtendedBattleReport.Value || Cfg.DlcCapturePrestige.Value;

        // ---------- capture tracking while a battle is finished ----------
        // Key: Autocalc instance id for auto-resolve, 0 for a manual battle.
        static readonly Dictionary<int, HashSet<int>> captured = new Dictionary<int, HashSet<int>>();
        static readonly Dictionary<int, HashSet<int>> garrison = new Dictionary<int, HashSet<int>>();
        internal static int? context;   // set while FinishLandBattle / SaveBattleResult runs

        internal static int Key(Autocalc ac) => ac == null ? 0 : ac.GetInstanceID();

        static HashSet<int> Set(Dictionary<int, HashSet<int>> d, int key)
        {
            if (!d.TryGetValue(key, out var s)) d[key] = s = new HashSet<int>();
            return s;
        }

        internal static void TrackCaptured(int key, int commander) => Set(captured, key).Add(commander);

        internal static void TrackGarrison(Regiment unit)
        {
            if (context == null || unit == null) return;
            var s = Set(garrison, context.Value);
            void add(Regiment r)
            {
                if (r != null && ValidCommander(r.commander) && GameVars.commander[r.commander].status == 0)
                    s.Add(r.commander);
            }
            add(unit);
            if (unit.allattachedunits != null)
                foreach (var r in unit.allattachedunits) add(r);
        }

        internal static bool ValidCommander(int c) => GameVars.commander != null && c >= 0 && c < GameVars.commander.Count;

        // ---------- sidecar ----------
        internal class Data
        {
            public int allianceWon = -1;
            public int[] alliance = { -1, -1 };
            public bool playerTookPart;
            public Dictionary<int, int> weapons = new Dictionary<int, int>();
            public List<int>[] kia = { new List<int>(), new List<int>() };
            public List<int>[] wia = { new List<int>(), new List<int>() };
            public List<int>[] pow = { new List<int>(), new List<int>() };
            public List<int>[] gar = { new List<int>(), new List<int>() };
            public float prestigeGained;
            public bool prestigeApplied;
        }

        internal static Data pending;

        static int AllianceOf(List<Regiment> l)
        {
            if (l != null) foreach (var r in l) if (r != null) return r.alliance;
            return -1;
        }

        static void AddRegs(List<Regiment> tops, List<Regiment> into)
        {
            if (tops == null) return;
            foreach (var t in tops)
            {
                if (t == null) continue;
                into.Add(t);
                if (t.allattachedunits != null) into.AddRange(t.allattachedunits.Where(r => r != null));
            }
        }

        internal static void WriteSidecar(int alliancewon, int[] capturedweapons, List<int> kia0, List<int> kia1,
            List<Regiment> tops0, List<Regiment> tops1, List<int> wia0, List<int> wia1)
        {
            var all = new List<Regiment>();
            AddRegs(tops0, all);
            AddRegs(tops1, all);
            if (all.Any(r => r.unittyp == 17 || r.fleetref != null))
            {
                // Naval engagement: no weapon captures, report not extended.
                if (context != null) { captured.Remove(context.Value); garrison.Remove(context.Value); }
                DeleteSidecar();
                return;
            }
            var participants = new HashSet<int>(all.Select(r => r.commander));

            var d = new Data { allianceWon = alliancewon };
            d.alliance[0] = AllianceOf(tops0);
            d.alliance[1] = AllianceOf(tops1);
            if (d.alliance[0] < 0 && d.alliance[1] >= 0 && d.alliance[1] <= 1) d.alliance[0] = 1 - d.alliance[1];
            if (d.alliance[1] < 0 && d.alliance[0] >= 0 && d.alliance[0] <= 1) d.alliance[1] = 1 - d.alliance[0];

            if (capturedweapons != null)
                for (int i = 0; i < capturedweapons.Length; i++)
                    if (capturedweapons[i] > 0) d.weapons[i] = capturedweapons[i];

            if (kia0 != null) d.kia[0].AddRange(kia0);
            if (kia1 != null) d.kia[1].AddRange(kia1);
            if (wia0 != null) d.wia[0].AddRange(wia0);
            if (wia1 != null) d.wia[1].AddRange(wia1);

            if (context != null)
            {
                captured.TryGetValue(context.Value, out var pows);
                garrison.TryGetValue(context.Value, out var gars);
                foreach (int c in pows ?? Enumerable.Empty<int>())
                {
                    if (!ValidCommander(c) || GameVars.commander[c].status != 2 || !participants.Contains(c)) continue;
                    int side = Array.IndexOf(d.alliance, GameVars.commander[c].alliance);
                    if (side >= 0) d.pow[side].Add(c);
                }
                foreach (int c in gars ?? Enumerable.Empty<int>())
                {
                    if (!ValidCommander(c)) continue;
                    int side = Array.IndexOf(d.alliance, GameVars.commander[c].alliance);
                    if (side >= 0 && !d.pow[side].Contains(c)) d.gar[side].Add(c);
                }
                captured.Remove(context.Value);
                garrison.Remove(context.Value);
            }

            // Did the career-DLC avatar's command take part?
            if (DLC_WL.dlc_scenarioactive && ValidCommander(DLC_WL.dlc_chosencommander))
            {
                int ps = Array.IndexOf(d.alliance, GameVars.playeralliance);
                var regs = new List<Regiment>();
                AddRegs(ps == 0 ? tops0 : ps == 1 ? tops1 : null, regs);
                d.playerTookPart = regs.Any(r => r.dlcw_isundercommander || r.commander == DLC_WL.dlc_chosencommander);
            }

            var sb = new StringBuilder();
            sb.AppendLine("1");
            sb.AppendLine(d.allianceWon.ToString());
            sb.AppendLine(d.alliance[0].ToString());
            sb.AppendLine(d.alliance[1].ToString());
            sb.AppendLine(d.playerTookPart ? "1" : "0");
            sb.AppendLine(string.Join(" ", d.weapons.Select(kv => kv.Key + ":" + kv.Value)));
            for (int s = 0; s < 2; s++)
            {
                sb.AppendLine(string.Join(" ", d.kia[s]));
                sb.AppendLine(string.Join(" ", d.wia[s]));
                sb.AppendLine(string.Join(" ", d.pow[s]));
                sb.AppendLine(string.Join(" ", d.gar[s]));
            }
            File.WriteAllText(SidecarPath, sb.ToString());
        }

        static List<int> Ints(string line) =>
            (line ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

        internal static Data ReadSidecar()
        {
            if (!File.Exists(SidecarPath)) return null;
            var l = File.ReadAllLines(SidecarPath);
            if (l.Length < 14 || l[0] != "1") return null;
            var d = new Data
            {
                allianceWon = int.Parse(l[1]),
                alliance = new[] { int.Parse(l[2]), int.Parse(l[3]) },
                playerTookPart = l[4] == "1",
            };
            foreach (var p in l[5].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = p.Split(':');
                d.weapons[int.Parse(kv[0])] = int.Parse(kv[1]);
            }
            for (int s = 0; s < 2; s++)
            {
                d.kia[s] = Ints(l[6 + s * 4]);
                d.wia[s] = Ints(l[7 + s * 4]);
                d.pow[s] = Ints(l[8 + s * 4]);
                d.gar[s] = Ints(l[9 + s * 4]);
            }
            return d;
        }

        internal static void DeleteSidecar()
        {
            try { if (File.Exists(SidecarPath)) File.Delete(SidecarPath); } catch { }
        }

        // ---------- DLC prestige ----------
        static bool ValidWeapon(int w) => GameVars.weapon != null && w >= 0 && w < GameVars.weapon.Length;

        // Same averaging as BattleUnits (~3820): active units up to brigade level, guns vs. the rest.
        static float VanillaAverage(int a, bool ari)
        {
            var list = GameVars.alliance[a].unitlist;
            if (list == null) return 0f;
            float sum = 0f; int n = 0;
            foreach (var u in list)
            {
                if (u == null || u.unittyp > 13 || !u.gameObject.activeInHierarchy || !ValidWeapon(u.weapon)) continue;
                if ((u.unittyp == 2) != ari) continue;
                sum += WeaponValue.Vanilla(u.weapon); n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        internal static void ApplyPrestige(Data d)
        {
            if (!Cfg.DlcCapturePrestige.Value || !DLC_WL.dlc_scenarioactive || !d.playerTookPart) return;
            int ps = Array.IndexOf(d.alliance, GameVars.playeralliance);
            if (ps < 0) return;
            int es = 1 - ps;
            var al = GameVars.alliance[GameVars.playeralliance];

            float weaponPts = 0f;
            if (d.allianceWon == GameVars.playeralliance)
                foreach (var kv in d.weapons)
                {
                    if (!ValidWeapon(kv.Key)) continue;
                    bool ari = GameVars.weapon[kv.Key].isari;
                    // Vanilla weapon rating (as for upgrade costs), against the side's vanilla-rated average.
                    float avg = VanillaAverage(GameVars.playeralliance, ari);
                    if (avg <= 0f) continue;
                    float excess = WeaponValue.Vanilla(kv.Key) / avg - 1f;
                    if (excess > 0f) weaponPts += excess * kv.Value * (ari ? GunWeight : 1f);
                }
            float weaponPrestige = Mathf.Min(weaponPts * WeaponPrestigeScale, WeaponPrestigeCap) * Cfg.PrestigePerCapturedWeaponValue.Value;

            float rankSum = 0f;
            // Each officer counts rank x (0.1 + fame). Raw fame (0-1) is used because
            // GetCommanderFame() is already reduced for a killed or captured commander.
            float Weight(int c) => Mathf.Max(0, GameVars.commander[c].rank) * (FameBase + Mathf.Max(0f, GameVars.commander[c].fame));
            foreach (int c in d.pow[es].Concat(d.gar[es])) if (ValidCommander(c)) rankSum += Weight(c);
            foreach (int c in d.kia[es].Concat(d.wia[es])) if (ValidCommander(c)) rankSum += 0.5f * Weight(c);
            float officerPrestige = rankSum * Cfg.PrestigePerOfficerRank.Value;

            float total = weaponPrestige + officerPrestige;
            if (total <= 0f) return;
            float before = DLC_WL.prestige;
            DLC_WL.AdjustPrestige(total, 1, "captured weapons and officers");
            d.prestigeGained = DLC_WL.prestige - before;
            d.prestigeApplied = true;
            BugFixPlugin.Log.LogInfo($"BattleReport: prestige +{d.prestigeGained:0} (weapons {weaponPrestige:0}, officers {officerPrestige:0}, before multipliers)");
        }

        // ---------- report text ----------
        static string Names(List<int> ids)
        {
            var valid = ids.Where(ValidCommander).ToList();
            var parts = valid.Take(MaxListed).Select(c => GameVars.commander[c].GetName(2, 0)).ToList();
            if (valid.Count > MaxListed) parts.Add("and " + (valid.Count - MaxListed) + " more");
            return string.Join(", ", parts);
        }

        static void Officers(StringBuilder sb, string who, Data d, int side)
        {
            if (d.kia[side].Count + d.wia[side].Count + d.pow[side].Count + d.gar[side].Count == 0) return;
            sb.Append("\n" + who + ":\n");
            if (d.kia[side].Count > 0) sb.Append("- Killed: " + Names(d.kia[side]) + "\n");
            if (d.wia[side].Count > 0) sb.Append("- Wounded: " + Names(d.wia[side]) + "\n");
            if (d.pow[side].Count > 0) sb.Append("- Captured: " + Names(d.pow[side]) + "\n");
            if (d.gar[side].Count > 0) sb.Append("- Surrendered with the garrison: " + Names(d.gar[side]) + "\n");
        }

        internal static string BuildText(Data d)
        {
            var sb = new StringBuilder();
            int ps = Array.IndexOf(d.alliance, GameVars.playeralliance);
            if (Cfg.ExtendedBattleReport.Value)
            {
                var w = d.weapons.Where(kv => ValidWeapon(kv.Key)).OrderByDescending(kv => GameVars.weapon[kv.Key].isari).ThenByDescending(kv => kv.Value).ToList();
                if (w.Count > 0)
                {
                    sb.Append(d.allianceWon == GameVars.playeralliance ? "\nWeapons captured by our forces:\n" : "\nWeapons lost to the enemy:\n");
                    foreach (var kv in w) sb.Append("- " + kv.Value.ToString("#,##0") + " x " + GameVars.weapon[kv.Key].name + "\n");
                }
                if (ps >= 0)
                {
                    Officers(sb, "Enemy officer casualties", d, 1 - ps);
                    Officers(sb, "Our officer casualties", d, ps);
                }
            }
            if (d.prestigeApplied && Mathf.Abs(d.prestigeGained) >= 0.5f)
                sb.Append("\nPrestige for captured weapons and enemy officers: " + Tools.GetAbrevStrValue(d.prestigeGained, 0, useplus: true) + "\n");
            return sb.ToString();
        }
    }

    // ---------- context ----------
    [HarmonyPatch(typeof(Autocalc), "FinishLandBattle")]
    static class BR_FinishLandBattle
    {
        static void Prefix(Autocalc __instance) { if (BattleReport.Enabled) BattleReport.context = BattleReport.Key(__instance); }
        static void Finalizer() => BattleReport.context = null;
    }

    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.SaveBattleResult))]
    static class BR_SaveBattleResult
    {
        static void Prefix() { if (BattleReport.Enabled) BattleReport.context = 0; }
        static void Finalizer() => BattleReport.context = null;
    }

    // ---------- officer capture tracking ----------
    [HarmonyPatch(typeof(Regiment), "UpdateCommanderCasualtyState")]
    static class BR_CommanderCasualty
    {
        static void Prefix(Regiment __instance, out int __state)
        {
            __state = BattleReport.ValidCommander(__instance.commander) ? GameVars.commander[__instance.commander].status : -1;
        }
        static void Postfix(Regiment __instance, Autocalc calledfromautocalc, int __state)
        {
            if (!BattleReport.Enabled || __state != 0 || !BattleReport.ValidCommander(__instance.commander)) return;
            if (GameVars.commander[__instance.commander].status == 2)
                BattleReport.TrackCaptured(BattleReport.Key(calledfromautocalc), __instance.commander);
        }
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.Surrender))]
    static class BR_Surrender
    {
        static void Prefix(Regiment __instance, out int __state)
        {
            __state = BattleReport.ValidCommander(__instance.commander) ? GameVars.commander[__instance.commander].status : -1;
        }
        static void Postfix(Regiment __instance, int __state)
        {
            if (!BattleReport.Enabled || __state != 0 || !BattleReport.ValidCommander(__instance.commander)) return;
            if (GameVars.commander[__instance.commander].status == 2)
                BattleReport.TrackCaptured(0, __instance.commander);
        }
    }

    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.SurrenderUnitCampaign), new[] { typeof(Regiment) })]
    static class BR_GarrisonSurrender
    {
        static void Prefix(Regiment unittosurrender)
        {
            if (BattleReport.Enabled) BattleReport.TrackGarrison(unittosurrender);
        }
    }

    // ---------- result export (both battle paths) ----------
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.ExportBattleResultData))]
    static class BR_Export
    {
        static void Postfix(int alliancewon, int[] capturedweapons, List<int> kiacommanders0, List<int> kiacommanders1,
            List<Regiment> topunitsside0, List<Regiment> topunitsside1, List<int> wiacommanders0, List<int> wiacommanders1)
        {
            if (!BattleReport.Enabled) return;
            try
            {
                BattleReport.WriteSidecar(alliancewon, capturedweapons, kiacommanders0, kiacommanders1,
                    topunitsside0, topunitsside1, wiacommanders0, wiacommanders1);
            }
            catch (Exception e) { BugFixPlugin.Log.LogWarning("BattleReport: could not write report data: " + e); }
        }
    }

    // ---------- campaign side ----------
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.WriteBattleResultToMonument))]
    static class BR_WriteResult
    {
        static void Prefix()
        {
            BattleReport.pending = null;
            if (!BattleReport.Enabled) return;
            // Vanilla bails out early without these; don't grant prestige for a result it will not process.
            if (GameObject.Find("UI") == null || GameObject.Find("GameController") == null) return;
            try
            {
                BattleReport.pending = BattleReport.ReadSidecar();
                if (BattleReport.pending != null) BattleReport.ApplyPrestige(BattleReport.pending);
            }
            catch (Exception e) { BugFixPlugin.Log.LogWarning("BattleReport: could not read report data: " + e); }
        }

        static void Finalizer()
        {
            if (BattleReport.pending != null) BattleReport.DeleteSidecar();
            BattleReport.pending = null;
        }
    }

    [HarmonyPatch(typeof(Messages.Message), nameof(Messages.Message.GenerateMessageContent))]
    static class BR_MessageContent
    {
        static void Postfix(Messages.Message __instance)
        {
            var d = BattleReport.pending;
            if (d == null || __instance.type != Messages.msg_combatresult || __instance.objref == null) return;
            try
            {
                string extra = BattleReport.BuildText(d);
                if (extra.Length > 0) __instance.content += "\n" + extra;
            }
            catch (Exception e) { BugFixPlugin.Log.LogWarning("BattleReport: could not build report text: " + e); }
        }
    }
}
