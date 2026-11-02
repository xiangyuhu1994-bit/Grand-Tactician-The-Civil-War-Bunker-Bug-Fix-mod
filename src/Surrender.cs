// Battle surrenders and pursuit. Write-up: notes/surrender.md

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class SurrenderHelpers
    {
        public static readonly AccessTools.FieldRef<Regiment, float> InMeleeNonRouted =
            AccessTools.FieldRefAccess<Regiment, float>("amountinmeleenonrouted");

        // The same sequence vanilla's UpdateSurrender starts when its timer expires: the unit
        // walks toward the enemy showing surrender sprites, fades, then calls Surrender().
        public static void BeginSurrender(Regiment r, string why)
        {
            if (r == null || r.surrenderstate != 0) return;
            r.surrenderstate = 1;
            r.surrendercounter = GameVars.currenttimefromstart + GamePrefs.showsurrenderspritestime;
            r.permanentlydetached = true;
            r.SetMovementMode(5);
            r.StopRegiment(skipfinalrotation: true, manualstop: true);
            var bfs = RegimentAccess.Bfs(r);
            if (bfs != null)
            {
                Vector3 p = bfs.GetAngleTerrainPos(r.GetPosition(), r.unitrange.retreatangle + 180f, 250f, RegimentAccess.Terrain(r));
                r.RegimentSetPath(p, newpath: true, skipcover: true, -1f, modifylastwaypoint: false, 2, -1f, null,
                                  autoadvancefallback: true, uimovement: false);
            }
            GameVars.DebugOwnLog(r + " surrendering (bugFixMod: " + why + ")");
        }

        public static float Speed(Regiment r, bool applyFatigue)
        {
            int t = Mathf.Clamp(r.unittyp, 0, GamePrefs.unitbasicspeed.GetLength(0) - 1);
            int m = Mathf.Clamp(r.mounted, 0, GamePrefs.unitbasicspeed.GetLength(1) - 1);
            float s = GamePrefs.unitbasicspeed[t, m];
            if (GamePrefs.runningspeedfact != null && t < GamePrefs.runningspeedfact.Length) s *= GamePrefs.runningspeedfact[t];
            if (applyFatigue) s *= Mathf.Pow(Mathf.Max(1f - r.fatigue, 0.01f), GamePrefs.fatiguefactorspeed);
            return s;
        }

        public static float LossRatio(Regiment r)
        {
            int lost = 0;
            if (r.losses != null) foreach (int l in r.losses) lost += l;
            return lost / (float)Mathf.Max(1, lost + r.strength);
        }
    }

    // ------------------------------------------------------------------------------
    // Surrender in melee almost never happens.
    // Vanilla arms the 0.22 h countdown only at the instant a unit routs while in melee
    // (Regiment.Rout ~14706). Breaking contact for 20 % of the countdown clears it
    // (UpdateSurrender ~15040), and nothing ever re-arms it, so a routed unit that gets away
    // once and is caught again can never surrender this way. It also needs unbroken contact
    // and to be in melee at the exact moment the countdown ends.
    // Fix: count melee time while routed cumulatively. It builds up while the routed unit is
    // in melee with a non-routed enemy and drains slowly out of contact. At the threshold the
    // unit begins the vanilla surrender sequence.
    // ------------------------------------------------------------------------------
    // ------------------------------------------------------------------------------
    // Feature: routed units can be run down.
    // A routed unit with faster, non-routed enemies close by can surrender without melee.
    // Chance per hour = RunDownChancePerHour x size factor x fatigue x losses x inexperience.
    // Size factor = min(1, total pursuer strength / routed strength)^1.5, so a small cavalry
    // unit can barely run down a large infantry brigade. Routed units run at full speed in
    // vanilla (no fatigue penalty), so in practice this is cavalry catching foot troops.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.UpdateSurrender))]
    static class SurrenderAccumulator_Patch
    {
        class State { public float melee; public float lastTime; public float nextRunDownCheck; }
        static readonly Dictionary<Regiment, State> States = new Dictionary<Regiment, State>();
        const float RunDownInterval = 0.01f;   // hours between run-down rolls (~36 s)

        static void Postfix(Regiment __instance)
        {
            var r = __instance;
            if (r == null || r.calledfromcampaign || r.unittyp > 13 || r.unittyp == 5) return;
            bool cumulative = Cfg.CumulativeMeleeSurrender.Value, runDown = Cfg.RunDownSurrender.Value;
            if (!cumulative && !runDown) return;

            if (!r.isrouted || r.surrenderstate != 0 || r.strength <= 0)
            {
                if (!r.isrouted) States.Remove(r);
                return;
            }
            float now = GameVars.currenttimefromstart;
            if (!States.TryGetValue(r, out var st))
            {
                st = new State { lastTime = now, nextRunDownCheck = now + RunDownInterval };
                States[r] = st;
            }
            float dt = Mathf.Clamp(now - st.lastTime, 0f, 0.05f);
            st.lastTime = now;

            if (cumulative)
            {
                bool inContact = r.inmelee > 0f && SurrenderHelpers.InMeleeNonRouted(r) > 0f;
                if (inContact) st.melee += dt;
                else st.melee = Mathf.Max(0f, st.melee - dt * Cfg.SurrenderMeleeDecay.Value);
                if (st.melee >= Cfg.SurrenderMeleeHours.Value)
                {
                    States.Remove(r);
                    SurrenderHelpers.BeginSurrender(r, "caught in melee too long while routed");
                    return;
                }
            }

            if (runDown && now >= st.nextRunDownCheck)
            {
                st.nextRunDownCheck = now + RunDownInterval;
                if (RollRunDown(r, RunDownInterval))
                {
                    States.Remove(r);
                    SurrenderHelpers.BeginSurrender(r, "run down while routed");
                }
            }
        }

        static bool RollRunDown(Regiment r, float hours)
        {
            if (r.unitrange == null || r.unitrange.enemyinrangereg == null) return false;
            float mySpeed = SurrenderHelpers.Speed(r, applyFatigue: false);   // routed units ignore fatigue in vanilla
            float range = Cfg.RunDownRange.Value;
            int pursuers = 0;
            foreach (var e in r.unitrange.enemyinrangereg)
            {
                if (e == null || e.isrouted || e.unittyp > 13 || e.unittyp == 2 || e.unittyp == 5 || e.strength <= 0) continue;
                if (Tools.GetXZDistance(e.GetPosition(), r.GetPosition()) > range) continue;
                if (SurrenderHelpers.Speed(e, applyFatigue: true) <= mySpeed * Cfg.RunDownSpeedMargin.Value) continue;
                pursuers += e.strength;
            }
            if (pursuers <= 0) return false;

            float size = Mathf.Pow(Mathf.Min(1f, pursuers / (float)Mathf.Max(1, r.strength)), 1.5f);
            float fatigue = 0.5f + Mathf.Clamp01(r.fatigue);
            float losses = 1f + SurrenderHelpers.LossRatio(r);
            float inexperience = 1.5f - Mathf.Clamp01(r.Experience());
            float perHour = Cfg.RunDownChancePerHour.Value * size * fatigue * losses * inexperience;
            float p = 1f - Mathf.Exp(-perHour * hours);
            return Random.value < p;
        }
    }

    // ------------------------------------------------------------------------------
    // Infantry gives up the chase.
    // In UpdateChargeTarget (Regiment.cs ~13350) a charge whose target routs is dropped at once
    // unless the charger is cavalry, so infantry can never close on a broken unit. Fix:
    // infantry (unittyp 0) keeps tracking a routed charge target while it is within
    // maxchargeradius of the charger and the charger's fatigue is below a limit. Reaching the
    // target, or exceeding either limit, falls back to vanilla (charge ends).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "UpdateChargeTarget")]
    static class InfantryPursuit_Patch
    {
        static bool Prefix(Regiment __instance)
        {
            if (!Cfg.InfantryPursuit.Value) return true;
            var r = __instance;
            if (r.unittyp != 0 || r.movementmode != 6 || r.chargetarget == null || r.regimentpaths <= 0) return true;
            if (!r.chargetarget.activeInHierarchy) return true;
            var t = r.chargetarget.GetComponent<Regiment>();
            if (t == null || !t.isrouted || t.surrenderstate != 0) return true;
            if (r.isrouted || r.fatigue > Cfg.InfantryPursuitMaxFatigue.Value) return true;
            if (Tools.GetXZDistance(r.GetPosition(), r.chargetarget.transform.position) > GamePrefs.maxchargeradius) return true;

            // Vanilla's own tracking for cavalry: re-path when the target has moved > 5 units.
            if (Tools.GetXZDistance(r.GetLastWaypointPos(), r.chargetarget.transform.position) > 5f)
                r.RegimentSetPath(r.chargetarget.transform.position, newpath: false, skipcover: true, -1f, modifylastwaypoint: true,
                                  2, -1f, null, autoadvancefallback: true, uimovement: false);
            return false;
        }
    }
}
