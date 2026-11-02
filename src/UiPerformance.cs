// UI performance: campaign OOB and save/load menu lag. Write-up: notes/ui_performance.md

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // Campaign OOB: one click rebuilds the OOB 6-9 times.
    // Opening the OOB (SetTab -> ChangeSelection(0)) calls SwitchUnitView(0) twice (each does
    // UpdateOOBs(overridelastvar: true)), UpdateContent -> UpdateOOBs() and
    // UpdateUnitData -> UpdateOOBs(). Each UpdateOOBs rebuilds all three OOB windows, including
    // the two split-view windows when they are hidden. Every rebuild instantiates a Canvas
    // prefab per unit, a "+" prefab per slot and a line object per link, and runs the full
    // UpdateGroupStrength (a campaign stats pass over the unit's whole subtree) twice per icon.
    //
    // Fix:
    //  - While inside SetTab / ChangeSelection / SwitchUnitView, OOB.UpdateOOB only records the
    //    requested unit. When the outermost of those returns, each OOB is built once with the
    //    last unit requested for it.
    //  - Split-view windows are not built while their panel is hidden. They are marked stale
    //    so they rebuild when shown (SwitchUnitView(2) shows them, then forces a rebuild).
    //  - During one OOB build, UpdateGroupStrength runs at most once per unit.
    // ------------------------------------------------------------------------------
    static class OobBatch
    {
        public static int Depth;
        public static bool Flushing;
        public static readonly Dictionary<OOB, GameObject> Pending = new Dictionary<OOB, GameObject>();
        public static readonly List<OOB> Order = new List<OOB>();
        public static readonly HashSet<OOB> SkippedHidden = new HashSet<OOB>();

        static readonly AccessTools.FieldRef<CampaignMainPanel, PanelHandler> PanelHandlerOf =
            AccessTools.FieldRefAccess<CampaignMainPanel, PanelHandler>("panelhandler");
        static readonly AccessTools.FieldRef<CampaignMainPanel, GameObject[]> LastUsed =
            AccessTools.FieldRefAccess<CampaignMainPanel, GameObject[]>("lastusedoobunit");

        static CampaignMainPanel cmp;
        public static CampaignMainPanel Cmp
        {
            get { if (cmp == null) cmp = Object.FindObjectOfType<CampaignMainPanel>(); return cmp; }
        }

        // Only the campaign split windows are ever skipped; everything else counts as visible.
        public static bool IsHiddenSplit(OOB oob)
        {
            var c = Cmp;
            if (c == null || c.oobs == null) return false;
            var ph = PanelHandlerOf(c);
            if (ph == null || ph.panel == null) return false;
            int idx = System.Array.IndexOf(c.oobs, oob);
            int panelId = idx == 1 ? ph.cpanel_oobsplit1 : idx == 2 ? ph.cpanel_oobsplit2 : -1;
            if (panelId < 0 || panelId >= ph.panel.Count || ph.panel[panelId] == null) return false;
            int s = ph.panel[panelId].state;
            return s != 1 && s != 2;   // 1 showing, 2 shown
        }

        public static void MarkStale(OOB oob)
        {
            var c = Cmp;
            if (c == null || c.oobs == null) return;
            var last = LastUsed(c);
            int idx = System.Array.IndexOf(c.oobs, oob);
            if (last != null && idx >= 0 && idx < last.Length) last[idx] = null;
        }

        public static void Enter() { if (Cfg.OobPerformance.Value) Depth++; }

        public static void Exit()
        {
            if (!Cfg.OobPerformance.Value || Depth <= 0) return;
            if (--Depth > 0) return;
            Flushing = true;
            try
            {
                foreach (var oob in Order)
                {
                    if (oob == null || !Pending.TryGetValue(oob, out var unit) || unit == null) continue;
                    try { oob.UpdateOOB(unit); }
                    catch (System.Exception e) { BugFixPlugin.Log.LogWarning("OOB flush: " + e); }
                }
            }
            finally
            {
                foreach (var oob in SkippedHidden) MarkStale(oob);
                SkippedHidden.Clear();
                Pending.Clear();
                Order.Clear();
                Flushing = false;
            }
        }
    }

    [HarmonyPatch(typeof(CampaignMainPanel), nameof(CampaignMainPanel.SetTab))]
    static class OobScope_SetTab
    {
        static void Prefix() => OobBatch.Enter();
        static void Finalizer() => OobBatch.Exit();
    }

    [HarmonyPatch(typeof(CampaignMainPanel), nameof(CampaignMainPanel.ChangeSelection))]
    static class OobScope_ChangeSelection
    {
        static void Prefix() => OobBatch.Enter();
        static void Finalizer() => OobBatch.Exit();
    }

    [HarmonyPatch(typeof(CampaignMainPanel), "SwitchUnitView")]
    static class OobScope_SwitchUnitView
    {
        static void Prefix() => OobBatch.Enter();
        static void Finalizer() => OobBatch.Exit();
    }

    [HarmonyPatch(typeof(OOB), nameof(OOB.UpdateOOB))]
    static class Oob_UpdateOOB_Patch
    {
        public static bool Building;
        public static readonly HashSet<Regiment> UpdatedThisBuild = new HashSet<Regiment>();
        public static readonly HashSet<Regiment> UpdatedThisBuildIgnoreActive = new HashSet<Regiment>();

        static bool Prefix(OOB __instance, GameObject unitforhierarchy)
        {
            if (!Cfg.OobPerformance.Value) return true;

            if (OobBatch.Depth > 0 && !OobBatch.Flushing)
            {
                if (!OobBatch.Pending.ContainsKey(__instance)) OobBatch.Order.Add(__instance);
                OobBatch.Pending[__instance] = unitforhierarchy;
                return false;
            }

            if (unitforhierarchy != null && OobBatch.IsHiddenSplit(__instance))
            {
                OobBatch.SkippedHidden.Add(__instance);
                return false;
            }

            Building = true;
            UpdatedThisBuild.Clear();
            UpdatedThisBuildIgnoreActive.Clear();
            return true;
        }

        static void Finalizer()
        {
            Building = false;
            UpdatedThisBuild.Clear();
            UpdatedThisBuildIgnoreActive.Clear();
        }
    }

    // UpdateOOBs stores lastusedoobunit after calling UpdateOOB; undo that for deferred or
    // skipped windows so the next ordinary UpdateOOBs() rebuilds them.
    [HarmonyPatch(typeof(CampaignMainPanel), nameof(CampaignMainPanel.UpdateOOBs))]
    static class Oob_UpdateOOBs_Patch
    {
        static void Postfix(CampaignMainPanel __instance)
        {
            if (!Cfg.OobPerformance.Value || __instance.oobs == null) return;
            foreach (var oob in __instance.oobs)
            {
                if (oob == null) continue;
                if (OobBatch.SkippedHidden.Remove(oob)) OobBatch.MarkStale(oob);
            }
        }
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.UpdateGroupStrength))]
    static class Oob_GroupStrengthMemo_Patch
    {
        static bool Prefix(Regiment __instance, bool ignoreactivestate)
        {
            if (!Oob_UpdateOOB_Patch.Building) return true;
            // Same unit and same mode already computed in this build: values are unchanged.
            return (ignoreactivestate ? Oob_UpdateOOB_Patch.UpdatedThisBuildIgnoreActive : Oob_UpdateOOB_Patch.UpdatedThisBuild).Add(__instance);
        }
    }

    // ------------------------------------------------------------------------------
    // Save/load menu builds its list twice and reloads flag sprites per row.
    // The Save and Load buttons call Saving.InitializeFilters() then UpdateCompleteList().
    // InitializeFilters(checkall: true) ticks every filter entry through CheckSelection, which
    // leaves Top/Mid/BottomFilter.updated = true. Next frame Saving.Update -> CheckFilterChange
    // sees the flags, runs InitializeFilters again and rebuilds the complete list a second time.
    // The list covers every save of every scenario: per save it opens version.dat and
    // scenario.dat twice, instantiates a row prefab, and calls Resources.LoadAll for the flag.
    //
    // Fix: clear the "updated" flags after the menu-opening InitializeFilters (checkall: true)
    // so the second rebuild doesn't happen; real filter clicks still set them and rebuild.
    // Cache the flag sprite arrays by path.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Saving), nameof(Saving.InitializeFilters))]
    static class SaveMenu_InitializeFilters_Patch
    {
        static void Postfix(Saving __instance, bool checkall)
        {
            if (!Cfg.SaveMenuPerformance.Value || !checkall) return;
            foreach (var f in new[] { __instance.TopFilter, __instance.MidFilter, __instance.BottomFilter })
                if (f != null) f.updated = false;
        }
    }

    [HarmonyPatch]
    static class SaveMenu_FlagCache_Patch
    {
        static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

        static bool Prepare() => Cfg.SaveMenuPerformance.Value;

        static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(Saving), "SaveGameList"), "CreateEntryObject");

        public static Sprite[] LoadAllCached(string path)
        {
            if (Cache.TryGetValue(path, out var s) && s != null && s.Length > 0 && s[0] != null) return s;
            s = Resources.LoadAll<Sprite>(path);
            Cache[path] = s;
            return s;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = AccessTools.FirstMethod(typeof(Resources), m => m.Name == "LoadAll" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1)?.MakeGenericMethod(typeof(Sprite));
            int n = 0;
            foreach (var ci in instructions)
            {
                if (target != null && ci.Calls(target))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = AccessTools.Method(typeof(SaveMenu_FlagCache_Patch), nameof(LoadAllCached));
                    yield return ci;
                    n++;
                    continue;
                }
                yield return ci;
            }
            BugFixPlugin.Log.LogInfo("Save menu flag cache sites: " + n);
        }
    }
}
