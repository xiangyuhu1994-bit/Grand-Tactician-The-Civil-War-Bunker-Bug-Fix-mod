// Player units ignoring plain move/formation orders ("path interruption" bugs).
// Write-up: notes/order_interruption.md
//
// Vanilla's contact system (Regiment.CheckEnemyContact, Regiment.cs ~8197-8445) stops a group
// that is making a plain move when one of its units comes within ~0.9 x fire range of an enemy,
// and marks the group "path interrupted". Three problems make it fire on player units with no
// visible enemy near and then never let go:
//   1. Regiment.GetClosestEnemyUnit(float, ...) (~14807) reads unitrange.closestenemyunitfar,
//      which UpdateUnitRangeFast (~13660) fills with no fog-of-war test. Its 7-unit hysteresis
//      (~13969) can also keep a routed or deactivated enemy as "closest".
//   2. pathinterrupted is only cleared (~8245) by a unit that is its own groupaiobject. In career
//      battles BattleUnits.AllocateUnitsAndObjectivesToAI (~7571) calls ChangeStance on the
//      player's command, whose AddUnitToPool(overwriteaigroups: true) (AIBattle.cs ~5194) gives
//      every unit of the command the top HQ as groupaiobject. A brigade or regiment the player
//      ordered is never its own groupaiobject, so its flag is never cleared in that battle.
//   3. A new player order does not clear it: SetWaypoint's clearinterruptionpaths only empties
//      saved paths (ClearPathInterrupted, ~8603). While the flag is set, unselected units on a
//      plain move stop whenever an enemy is within 0.8 x fire range (~8265), group formations
//      are laid out unsorted (BattleUnits ~14273) and unitwhogavelastmovingorder is frozen
//      (~22210).
// Fix:
//   (a) While CheckEnemyContact / SetInterruptedStatusOfSubordinates run, GetClosestEnemyUnit
//       ignores routed/inactive enemies, and for the player's units also enemies hidden in fog.
//       If the cached closest enemy is rejected, the closest acceptable one is taken from
//       unitrange.enemyinrangereg (already fog-filtered, radius >= 150 units).
//   (b) A fresh player move (SetWaypoint from the UI) or formation order clears the flag on the
//       units it reaches, unless a visible enemy is within that unit's own interruption distance
//       (then it is a real contact and vanilla behaviour stays).
//   (c) A player unit that vanilla cannot un-interrupt (not its own groupaiobject) clears its
//       flag once no visible enemy is within the vanilla cancel distance (150 units).

using System;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class InterruptionCtx
    {
        [ThreadStatic] public static int Depth;

        // A unit the player commands (in career battles: one under the player's commander;
        // friendly AI units there share the player's alliance but keep vanilla behaviour).
        public static bool PlayerControlled(Regiment u)
        {
            if (u.alliance != GameVars.playeralliance || GameVars.ai_vs_ai || GameVars.debug_allarmiesai) return false;
            if (DLC_WL.dlc_scenarioactive && !u.dlcw_isundercommander) return false;
            return true;
        }

        public static bool AppliesFog(Regiment caller)
        {
            return Cfg.OrderInterruptionVisibleOnly.Value && GameVars.usefow && PlayerControlled(caller);
        }

        public static bool Acceptable(Regiment enemy, bool fog)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.isrouted || enemy.strength <= 0) return false;
            if (fog && enemy.iswithinfog && enemy.alliance != GameVars.playeralliance) return false;
            return true;
        }

        // Closest visible, fighting enemy within maxdistance (same distance measure as vanilla).
        public static GameObject Closest(Regiment self, float maxdistance, bool mid, bool coys, bool fog)
        {
            var list = self.unitrange?.enemyinrangereg;
            if (list == null) return null;
            Regiment best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!Acceptable(e, fog) || e.alliance == self.alliance) continue;
                float d = BattleUnits.GetClosestDistance(self, e, mid, coys);
                if (d <= maxdistance && d < bestD) { bestD = d; best = e; }
            }
            return best != null ? best.gameObject : null;
        }

        // Vanilla interruption distance for this unit (CheckEnemyContact ~8318).
        public static float StopDistance(Regiment u)
        {
            float fr;
            if (u.unittyp <= 13) fr = u.GetFireRange(true);
            else
            {
                fr = 9999f;
                foreach (var s in u.allattachedunits)
                    if (s != null && s.unittyp <= 13 && s.unittyp != 5 && s.weapon >= 0)
                        fr = Mathf.Min(fr, s.GetFireRange(true));
                if (fr >= 9999f) fr = 0f;
            }
            float d = fr * GamePrefs.distancefactoroffirerangetostopunitforinterruption;
            if (u.unittyp > 13) d = Mathf.Max(d, GamePrefs.distnacetoenemyforcommanderinterruption);
            return Mathf.Min(d, GamePrefs.distancetoenemyforinterruption);
        }

        public static bool VisibleEnemyWithin(Regiment u, float dist)
        {
            Depth++;
            try { return u.GetClosestEnemyUnit(dist, true, true) != null; }
            finally { Depth--; }
        }

        // (b): clear a stale flag on the units a fresh player order reaches.
        public static void FreshOrder(Regiment reg)
        {
            if (reg == null || !Cfg.OrderInterruptionStaleClear.Value) return;
            if (!PlayerControlled(reg)) return;
            var units = reg.allattachedunits;
            if (units == null || units.Length == 0) units = new[] { reg };
            foreach (var u in units)
            {
                if (u == null || !u.pathinterrupted || u.ai_stance > 0 || !PlayerControlled(u)) continue;
                if (u != reg && u.permanentlydetached) continue;
                if (VisibleEnemyWithin(u, StopDistance(u))) continue;   // real contact: keep vanilla
                u.SetPathInterrupted(false);
            }
        }
    }

    // (a) visible / fighting enemies only, inside the interruption code.
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.GetClosestEnemyUnit), new[] { typeof(float), typeof(bool), typeof(bool) })]
    static class InterruptionClosestEnemy_Patch
    {
        static void Postfix(Regiment __instance, float maxdistance, bool usemiddleofcenterandflank, bool usesinglecoys, ref GameObject __result)
        {
            if (InterruptionCtx.Depth <= 0 || !Cfg.OrderInterruptionVisibleOnly.Value) return;
            var far = __instance.unitrange?.closestenemyunitfar;
            if (far == null) return;                       // no enemy at all
            bool fog = InterruptionCtx.AppliesFog(__instance);
            var farReg = far.GetComponent<Regiment>();
            if (InterruptionCtx.Acceptable(farReg, fog)) return;   // vanilla answer stands
            __result = InterruptionCtx.Closest(__instance, maxdistance, usemiddleofcenterandflank, usesinglecoys, fog);
        }
    }

    [HarmonyPatch(typeof(Regiment), "CheckEnemyContact")]
    static class InterruptionCheckEnemyContact_Patch
    {
        static void Prefix(out bool __state)
        {
            __state = Cfg.OrderInterruptionVisibleOnly.Value;
            if (__state) InterruptionCtx.Depth++;
        }

        // (c) units vanilla can never un-interrupt.
        static void Postfix(Regiment __instance)
        {
            if (!Cfg.OrderInterruptionStaleClear.Value) return;
            var u = __instance;
            if (!u.pathinterrupted || !InterruptionCtx.PlayerControlled(u)) return;
            if (u.unittyp == 5 || u.ai_stance > 0 || u.groupaiobject == u.gameObject) return;
            if (InterruptionCtx.VisibleEnemyWithin(u, GamePrefs.distancetoenemytocancelinterruption)) return;
            u.SetPathInterrupted(false);
        }

        static void Finalizer(bool __state)
        {
            if (__state && InterruptionCtx.Depth > 0) InterruptionCtx.Depth--;
        }
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.SetInterruptedStatusOfSubordinates))]
    static class InterruptionSubordinateStatus_Patch
    {
        static void Prefix(out bool __state)
        {
            __state = Cfg.OrderInterruptionVisibleOnly.Value;
            if (__state) InterruptionCtx.Depth++;
        }

        static void Finalizer(bool __state)
        {
            if (__state && InterruptionCtx.Depth > 0) InterruptionCtx.Depth--;
        }
    }

    // (b) fresh player move orders (only the UI calls SetWaypoint with showmovementoptions).
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.SetWaypoint), new[] {
        typeof(Regiment), typeof(Vector3), typeof(bool), typeof(bool), typeof(float), typeof(bool), typeof(bool),
        typeof(float), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    static class InterruptionFreshMove_Patch
    {
        static void Prefix(Regiment reg, bool showmovementoptions, bool clearinterruptionpaths)
        {
            if (!showmovementoptions || !clearinterruptionpaths) return;
            InterruptionCtx.FreshOrder(reg);
        }
    }

    // (b) fresh player formation orders (the AI calls SetFormation with exitifsameformation: true).
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.SetFormation), new[] { typeof(GameObject), typeof(int), typeof(bool) })]
    static class InterruptionFreshFormation_Patch
    {
        static void Prefix(GameObject selectedunit, bool exitifsameformation)
        {
            if (exitifsameformation || selectedunit == null) return;
            InterruptionCtx.FreshOrder(selectedunit.GetComponent<Regiment>());
        }
    }
}
