// Fleet disintegration loop fix: a beaten fleet with no retreat path is teleported ("disintegrates,
// fleet survives") to a friendly harbor even when an enemy is sitting there, so it is caught again,
// renews its retreat and teleports back – forever.
// Write-up: notes/fleet_disintegration.md

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.DisintegrateUnit))]
    static class FleetDisintegration_Patch
    {
        // Same radius test the game uses to renew a retreat (Regiment.CheckEnemyContactCampaign:
        // GetClosestEnemyUnitReg(max(own bugle range, enemy bugle range * 1.1))), plus a margin.
        const float SafetyMargin = 1.25f;
        const float DaysPerYear = 365.25f;

        // Last teleport ("fleet survives") per fleet, in campaign days. Not saved: after loading a
        // game the first teleport is always allowed again (the no-safe-harbor test is stateless).
        static readonly Dictionary<Regiment, float> lastTeleportDay = new Dictionary<Regiment, float>();

        static float NowDays => GameVars.currenttimefromstart * DaysPerYear;

        static void Prefix(Regiment __instance, ref IIP preventfleetharbor)
        {
            try
            {
                if (!Cfg.FleetDisintegrationLoopFix.Value) return;
                Regiment fleet = __instance;
                if (fleet == null || fleet.unittyp != 17) return;

                if (preventfleetharbor == null)
                {
                    // Vanilla "no harbor" path: the fleet is deleted anyway; leave it untouched.
                    lastTeleportDay.Remove(fleet);
                    return;
                }

                Prune();
                float now = NowDays;
                float days = Mathf.Max(0f, Cfg.FleetDisintegrationRepeatDays.Value);

                // (b) Loop breaker: second teleport within N campaign days.
                if (lastTeleportDay.TryGetValue(fleet, out float last) && now >= last && now - last < days)
                {
                    BugFixPlugin.Log.LogInfo("Fleet " + fleet.name + " disintegrated again " + (now - last).ToString("0.0") +
                        " days after its last escape to a harbor – removing it instead of teleporting it again.");
                    Destroy(fleet, ref preventfleetharbor);
                    return;
                }

                // (a) Destination check: never teleport onto an enemy.
                IIP target = preventfleetharbor;
                if (!IsSafe(target, fleet))
                {
                    IIP alt = FindSafeHarbor(fleet, target);
                    if (alt == null)
                    {
                        BugFixPlugin.Log.LogInfo("Fleet " + fleet.name + " has no friendly harbor free of enemies (vanilla target " +
                            target.name + ") – removing it instead of teleporting it into another fight.");
                        Destroy(fleet, ref preventfleetharbor);
                        return;
                    }
                    BugFixPlugin.Log.LogInfo("Fleet " + fleet.name + ": enemy at " + target.name + ", escaping to " + alt.name + " instead.");
                    preventfleetharbor = alt;
                }
                lastTeleportDay[fleet] = now;
            }
            catch (System.Exception e)
            {
                BugFixPlugin.Log.LogError("FleetDisintegration prefix: " + e);
            }
        }

        // Let vanilla run its own "fleet disintegrates" path (DisintegrateUnit with no harbor ->
        // BattleUnits.DeleteFleet -> ships to the alliance pool, commander removed from command,
        // unit deleted). Optionally sink the ships first.
        static void Destroy(Regiment fleet, ref IIP harbor)
        {
            harbor = null;
            lastTeleportDay.Remove(fleet);
            MaybeScuttle(fleet);
        }

        static void MaybeScuttle(Regiment fleet)
        {
            if (!Cfg.FleetDisintegrationScuttleShips.Value) return;
            Fleet f = fleet.fleetref;
            if (f == null || f.attachedships == null) return;
            Vessel[] ships = (Vessel[])f.attachedships.Clone();
            foreach (var v in ships)
                if (v != null) v.SinkShip();
            // SinkShip uses a deferred Destroy, so the vessels are still children this frame;
            // clear the array so DeleteFleet doesn't move the scuttled ships into the pool.
            f.attachedships = new Vessel[0];
            BugFixPlugin.Log.LogInfo("Fleet " + fleet.name + ": " + ships.Length + " ship(s) scuttled.");
        }

        // A harbor is unsafe when an active enemy unit (any type) is close enough that the fleet,
        // once teleported there, would immediately renew its retreat or be caught in battle.
        internal static bool IsSafe(IIP harbor, Regiment fleet)
        {
            if (harbor == null) return false;
            if (harbor.allianceowner != fleet.alliance) return false;
            List<Regiment> units = BattleUnits.campaignunitlist ?? BattleUnits.completeunitlist;
            if (units == null) return true;
            Vector3 hp = harbor.transform.position;
            var me = GameVars.alliance[fleet.alliance];
            for (int i = 0; i < units.Count; i++)
            {
                Regiment u = units[i];
                if (u == null || u == fleet || !u.gameObject.activeInHierarchy) continue;
                if (u.alliance < 0 || !me.IsAtWarWith(u.alliance)) continue;
                if (u.unittyp == 17 && (u.fleetref == null || u.fleetref.attachedships == null || u.fleetref.attachedships.Length == 0)) continue;
                float radius = Mathf.Max(fleet.buglerange, u.buglerange * 1.1f) * SafetyMargin;
                if (Tools.GetXZDistance(u.transform.position, hp) < radius) return false;
            }
            return true;
        }

        // Closest friendly harbor (to the fleet) on the same kind of water as vanilla's choice
        // (sea / river, same test as BattleUnits.GetClosestHarbor) with no enemy nearby.
        static IIP FindSafeHarbor(Regiment fleet, IIP vanillaTarget)
        {
            List<IIP> harbors = BattleUnits.harbors;
            if (harbors == null) return null;
            int[] water = { 8, 4 };
            int wantTerrain = TerrainOf(vanillaTarget, water);
            Vector3 pos = fleet.transform.position;

            var candidates = new List<IIP>();
            foreach (var h in harbors)
                if (h != null && h != vanillaTarget && h.allianceowner == fleet.alliance)
                    candidates.Add(h);
            candidates.Sort((a, b) => Tools.GetXZDistance(pos, a.transform.position)
                .CompareTo(Tools.GetXZDistance(pos, b.transform.position)));

            foreach (var h in candidates)
            {
                if (!IsSafe(h, fleet)) continue;
                if (TerrainOf(h, water) != wantTerrain) continue;
                return h;
            }
            return null;
        }

        static int TerrainOf(IIP h, int[] water)
        {
            var t = BattlefieldSetup.SearchTerrainInRangePos(h.transform.position, water, 10);
            return t == null ? -1 : t.terrain;
        }

        static void Prune()
        {
            if (lastTeleportDay.Count < 64) return;
            var dead = new List<Regiment>();
            foreach (var k in lastTeleportDay.Keys)
                if (k == null) dead.Add(k);
            foreach (var k in dead) lastTeleportDay.Remove(k);
        }
    }
}
