// bugFixMod – multi-day battle freeze (EndOfDayFreeze) and battle save cross-references (SaveReferences).
// See notes/save_and_eod.md for the write-up.

using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    // ---------------------------------------------------------------------------------------------
    // Bug 1 – end-of-day state machine getting stuck (BattleUnits.eodcycle never returns to 0)
    // ---------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.CheckTimeIssues))]
    internal static class EndOfDayFreezePatch
    {
        static readonly AccessTools.FieldRef<BattleUnits, BattleUI> BuiRef =
            AccessTools.FieldRefAccess<BattleUnits, BattleUI>("bui");
        static readonly AccessTools.FieldRef<BattleUnits, PanelHandler> PanelRef =
            AccessTools.FieldRefAccess<BattleUnits, PanelHandler>("panelhandler");
        static readonly AccessTools.FieldRef<BattleUnits, TimePanel> TimeRef =
            AccessTools.FieldRefAccess<BattleUnits, TimePanel>("timepanel");

        const float Grace = 1f; // real seconds a bad state must persist before we act

        static float stalePromptSince = -1f;
        static float orphanSixSince = -1f;
        static float runningDeploySince = -1f;
        static bool errorLogged;

        static void Postfix(BattleUnits __instance)
        {
            if (!Cfg.EndOfDayFreeze.Value || __instance == null) return;
            try
            {
                Check(__instance);
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    BugFixPlugin.Log.LogWarning("EndOfDayFreeze: check failed: " + e);
                }
            }
        }

        static void Check(BattleUnits bu)
        {
            float now = Time.unscaledTime;
            int cyc = bu.eodcycle;
            if (bu.IsCampaign || cyc <= 0 || bu.endbattle >= 0)
            {
                stalePromptSince = orphanSixSince = runningDeploySince = -1f;
                return;
            }
            BattleUI bui = BuiRef(bu);
            PanelHandler ph = PanelRef(bu);
            if (bui == null || ph == null) return;
            TimePanel tp = TimeRef(bu);
            if (tp != null && tp.allianceretreating >= 0) return; // retreat in progress – vanilla owns the clock

            // (a) Phases 1, 4 and 5 only advance when prompt_panelopen == -1. If the prompt panel was
            //     hidden through a path that does not reset prompt_panelopen (e.g. the Panel overload
            //     of PanelHandler.HidePanel used by ClosePanel), the id stays set forever. Clear the
            //     stale id so the vanilla state machine continues by itself.
            if ((cyc == 1 || cyc == 4 || cyc == 5) && ph.prompt_panelopen != -1 && !PromptVisible(ph))
            {
                if (stalePromptSince < 0f) stalePromptSince = now;
                else if (now - stalePromptSince > Grace)
                {
                    BugFixPlugin.Log.LogWarning("EndOfDayFreeze: prompt " + ph.prompt_panelopen +
                        " is registered as open but not shown (eodcycle " + cyc + "); clearing it.");
                    ph.prompt_panelopen = -1;
                    stalePromptSince = -1f;
                }
            }
            else stalePromptSince = -1f;

            // (b) Terminal state 6 without deployment mode and without a pending cease-fire.
            //     Vanilla only ever leaves 6 through SetActiveDeploymentPhase(false), which needs
            //     deploymentmodeactive == true. With a cease-fire (triggerendofday) state 6 is a
            //     legitimate "fighting halted until night" phase, so it is left alone.
            if (cyc == 6 && !bui.deploymentmodeactive && !bu.triggerendofday)
            {
                if (orphanSixSince < 0f) orphanSixSince = now;
                else if (now - orphanSixSince > Grace)
                {
                    BugFixPlugin.Log.LogWarning("EndOfDayFreeze: eodcycle stuck at 6 with no deployment phase active; resuming the battle.");
                    FinishEndOfDay(bu);
                    orphanSixSince = -1f;
                }
            }
            else orphanSixSince = -1f;

            // (c) Deployment phase (6 + deploymentmodeactive) while the clock runs. Several UI paths
            //     resume time with GameVars.SetGameSpeed() without calling SetActiveDeploymentPhase(false)
            //     (closing the in-game menu, timed-movement panel, cancelling a retreat prompt...).
            //     Units are then frozen while time passes. We do NOT end deployment for the player –
            //     we pause again so the deployment phase stays intact and <play> ends it as intended.
            if (cyc == 6 && bui.deploymentmodeactive && !bu.triggerendofday && !GameVars.gamepaused)
            {
                if (runningDeploySince < 0f) runningDeploySince = now;
                else if (now - runningDeploySince > Grace)
                {
                    BugFixPlugin.Log.LogWarning("EndOfDayFreeze: time was resumed during the deployment phase without ending it; pausing. Press play to start the day.");
                    GameVars.SetGameSpeed(0f);
                    runningDeploySince = -1f;
                }
            }
            else runningDeploySince = -1f;
        }

        static bool PromptVisible(PanelHandler ph)
        {
            int idx = ph.panel_prompt;
            if (ph.panel == null || idx < 0 || idx >= ph.panel.Count) return true; // unknown – assume visible
            var p = ph.panel[idx];
            if (p == null || p.go == null) return false;
            return p.state > 0 || p.go.activeInHierarchy;
        }

        // The parts of BattleUI.SetActiveDeploymentPhase(false) that matter when no deployment
        // phase is actually running: clear eodcycle and re-enable the bits it gates.
        static void FinishEndOfDay(BattleUnits bu)
        {
            bu.eodcycle = 0;
            GameVars.deactivateshortcuts = false;
            GameVars.showsupplylines = false;
            try
            {
                if (bu.aibattle != null && bu.aibattle.Length > 0 && bu.aibattle[0] != null)
                    bu.aibattle[0].AICheckInitialPontoonConstruction();
            }
            catch (Exception e)
            {
                BugFixPlugin.Log.LogWarning("EndOfDayFreeze: AICheckInitialPontoonConstruction failed: " + e.Message);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Bug 2 – battle save cross-references (detachments losing their parent link)
    // ---------------------------------------------------------------------------------------------
    internal static class SaveRefState
    {
        public static int Fallbacks, Misses;
    }

    // Vanilla matches a saved reference only if name AND position (rounded to a 0.5 grid) agree.
    // Units are nudged by StopRegiment/ChangeRegimentFormation while battleunits.dat is still being
    // read, and grid rounding fails for points near a cell edge, so the match silently returns null.
    // Same file format: we read the same four lines, try the vanilla match first, then fall back
    // to the name (unique name, or nearest same-named unit within FallbackRadius).
    [HarmonyPatch(typeof(ImportExportUnitData), "GetRegGameObjectFromPosition")]
    internal static class SaveRefResolverPatch
    {
        const float FallbackRadius = 30f;

        static bool Prefix(List<Regiment> regs, StreamReader file, float accuracy, ref GameObject __result)
        {
            if (!Cfg.SaveReferences.Value) return true;

            // Read exactly what vanilla reads (same parsing, so failures behave the same).
            string name = file.ReadLine();
            Vector3 pos = new Vector3(float.Parse(file.ReadLine()), float.Parse(file.ReadLine()), float.Parse(file.ReadLine()));
            __result = null;
            try
            {
                __result = Resolve(regs, name, pos, accuracy);
            }
            catch (Exception e)
            {
                BugFixPlugin.Log.LogWarning("SaveReferences: resolving '" + name + "' failed: " + e.Message);
            }
            return false;
        }

        static GameObject Resolve(List<Regiment> regs, string name, Vector3 pos, float accuracy)
        {
            if (regs == null) return null;
            // 1) vanilla rule
            for (int i = 0; i < regs.Count; i++)
            {
                Regiment r = regs[i];
                if (r == null) continue;
                GameObject go = r.gameObject;
                if (Tools.CompareVector3(go.transform.position, pos, accuracy, excludey: true) && name == go.name)
                    return go;
            }
            if (name == null || name == "Null") return null;

            // 2) fallback by name
            GameObject best = null;
            float bestDist = float.MaxValue;
            int count = 0;
            for (int i = 0; i < regs.Count; i++)
            {
                Regiment r = regs[i];
                if (r == null || r.gameObject.name != name) continue;
                count++;
                Vector3 p = r.gameObject.transform.position;
                float dx = p.x - pos.x, dz = p.z - pos.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestDist) { bestDist = d; best = r.gameObject; }
            }
            if (count == 1 || (best != null && bestDist <= FallbackRadius))
            {
                SaveRefState.Fallbacks++;
                return best;
            }
            SaveRefState.Misses++;
            return null;
        }
    }

    // After battleunits.dat is read: repair parentregiment/detachedregiment pairs and the hierarchy.
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.LoadBattleUnitData))]
    internal static class SaveRefRelinkPatch
    {
        const string Suffix = "'s Detachment";

        static void Prefix()
        {
            SaveRefState.Fallbacks = 0;
            SaveRefState.Misses = 0;
        }

        static void Postfix()
        {
            if (!Cfg.SaveReferences.Value) return;
            try
            {
                Relink();
            }
            catch (Exception e)
            {
                BugFixPlugin.Log.LogWarning("SaveReferences: relink pass failed: " + e);
            }
            if (SaveRefState.Fallbacks > 0 || SaveRefState.Misses > 0)
                BugFixPlugin.Log.LogInfo("SaveReferences: " + SaveRefState.Fallbacks + " unit reference(s) recovered by name, " +
                    SaveRefState.Misses + " unresolved.");
        }

        static void Relink()
        {
            GameObject unitsRoot = GameObject.Find("Battlemap/Units");
            if (unitsRoot == null) return;
            Regiment[] all = unitsRoot.GetComponentsInChildren<Regiment>(true);
            int linked = 0;
            var touched = new List<KeyValuePair<Regiment, Regiment>>(); // (parent, detachment)

            // 1) one side of the pair resolved – restore the other side
            foreach (Regiment d in all)
            {
                if (d == null || d.parentregiment == null) continue;
                Regiment p = d.parentregiment.GetComponent<Regiment>();
                if (p == null || p == d) continue;
                touched.Add(new KeyValuePair<Regiment, Regiment>(p, d));
                if (p.detachedregiment == null)
                {
                    p.detachedregiment = d.gameObject;
                    linked++;
                }
            }
            foreach (Regiment p in all)
            {
                if (p == null || p.detachedregiment == null) continue;
                Regiment d = p.detachedregiment.GetComponent<Regiment>();
                if (d == null || d == p) continue;
                if (d.parentregiment == null)
                {
                    d.parentregiment = p.gameObject;
                    touched.Add(new KeyValuePair<Regiment, Regiment>(p, d));
                    linked++;
                }
            }

            // 2) both sides lost – match "<Commander>'s Detachment" to the regiment with the same commander
            foreach (Regiment d in all)
            {
                if (d == null || d.parentregiment != null) continue;
                if (d.unittyp < 2 || d.unittyp > 4) continue;
                string dn = d.gameObject.name;
                if (dn == null || !dn.EndsWith(Suffix, StringComparison.Ordinal)) continue;
                if (!CommanderNameMatches(d.commander, dn)) continue;

                Regiment best = null;
                float bestDist = float.MaxValue;
                foreach (Regiment p in all)
                {
                    if (p == null || p == d) continue;
                    if (p.commander != d.commander || p.alliance != d.alliance) continue;
                    if (p.unittyp > 2 || p.parentregiment != null || p.detachedregiment != null) continue;
                    string pn = p.gameObject.name;
                    if (pn != null && pn.EndsWith(Suffix, StringComparison.Ordinal)) continue;
                    float dist = Vector3.Distance(p.transform.position, d.transform.position);
                    if (dist < bestDist) { bestDist = dist; best = p; }
                }
                if (best == null)
                {
                    BugFixPlugin.Log.LogWarning("SaveReferences: no parent regiment found for '" + dn + "' (commander " + d.commander + ").");
                    continue;
                }
                d.parentregiment = best.gameObject;
                best.detachedregiment = d.gameObject;
                touched.Add(new KeyValuePair<Regiment, Regiment>(best, d));
                linked++;
            }

            // 3) put each detachment back beside its parent regiment in the hierarchy
            int moved = 0;
            foreach (var kv in touched)
            {
                Regiment p = kv.Key, d = kv.Value;
                try
                {
                    Transform target = p.transform.parent;
                    if (target != null && d.transform.parent != target)
                    {
                        Transform old = d.transform.parent;
                        d.transform.parent = target; // keeps world position
                        moved++;
                        Regiment oldGrp = old != null ? old.GetComponent<Regiment>() : null;
                        if (oldGrp != null) BattleUnits.UpdateAllParentUnits(oldGrp.gameObject, startwithstatedunit: true);
                        BattleUnits.UpdateAllParentUnits(p.gameObject);
                    }
                    // commander list: the parent, not the detachment, holds the command
                    if (d.commander >= 0 && GameVars.commander != null && d.commander < GameVars.commander.Count &&
                        GameVars.commander[d.commander] != null && GameVars.commander[d.commander].currentcommand == d)
                        GameVars.commander[d.commander].currentcommand = p;
                    d.UpdateUnitSymbol();
                }
                catch (Exception e)
                {
                    BugFixPlugin.Log.LogWarning("SaveReferences: re-parenting '" + d.gameObject.name + "' failed: " + e.Message);
                }
            }

            if (linked > 0 || moved > 0)
                BugFixPlugin.Log.LogInfo("SaveReferences: restored " + linked + " detachment link(s), re-parented " + moved + " detachment(s).");
        }

        static bool CommanderNameMatches(int cmd, string unitName)
        {
            if (GameVars.commander == null || cmd < 0 || cmd >= GameVars.commander.Count) return false;
            var c = GameVars.commander[cmd];
            return c != null && c.name != null && unitName == c.name + Suffix;
        }
    }

    // ReattachCompany adds the parent's own prisoner count to itself instead of the detachment's
    // (BattleUnits.cs ~12863: inflictedprisonersperunittype[l] += component2...[l]).
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.ReattachCompany))]
    internal static class ReattachPrisonersPatch
    {
        internal class State
        {
            public Regiment Parent;
            public int[] ParentBefore, DetBefore;
        }

        static void Prefix(GameObject regiment, out State __state)
        {
            __state = null;
            if (!Cfg.SaveReferences.Value || regiment == null) return;
            try
            {
                Regiment r = regiment.GetComponent<Regiment>();
                if (r == null) return;
                GameObject pGo = null, dGo = null;
                if (r.parentregiment != null) { pGo = r.parentregiment; dGo = regiment; }
                if (r.detachedregiment != null) { pGo = regiment; dGo = r.detachedregiment; }
                if (pGo == null || dGo == null) return;
                Regiment p = pGo.GetComponent<Regiment>(), d = dGo.GetComponent<Regiment>();
                if (p == null || d == null || p.inflictedprisonersperunittype == null || d.inflictedprisonersperunittype == null) return;
                __state = new State
                {
                    Parent = p,
                    ParentBefore = (int[])p.inflictedprisonersperunittype.Clone(),
                    DetBefore = (int[])d.inflictedprisonersperunittype.Clone()
                };
            }
            catch (Exception e)
            {
                BugFixPlugin.Log.LogWarning("SaveReferences: ReattachCompany prefix failed: " + e.Message);
            }
        }

        static void Postfix(int __result, State __state)
        {
            if (__state == null || __result != 0 || __state.Parent == null) return;
            try
            {
                int[] arr = __state.Parent.inflictedprisonersperunittype;
                int n = Math.Min(arr.Length, Math.Min(__state.ParentBefore.Length, __state.DetBefore.Length));
                for (int i = 0; i < n; i++)
                    arr[i] = __state.ParentBefore[i] + __state.DetBefore[i];
            }
            catch (Exception e)
            {
                BugFixPlugin.Log.LogWarning("SaveReferences: ReattachCompany postfix failed: " + e.Message);
            }
        }
    }
}
