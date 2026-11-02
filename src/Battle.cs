// Battle fixes: artillery line of fire, combat behaviour wiped by attack moves, cavalry
// "off" buttons, skirmisher fallback geometry, routed detachments running through enemies,
// column back-step on new paths, charge end losing waypoints.
// Write-up: notes/battle.md

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class RegimentAccess
    {
        public static readonly AccessTools.FieldRef<Regiment, BattlefieldSetup> Bfs =
            AccessTools.FieldRefAccess<Regiment, BattlefieldSetup>("bfs");
        public static readonly AccessTools.FieldRef<Regiment, Terrain> Terrain =
            AccessTools.FieldRefAccess<Regiment, Terrain>("terrain");
    }

    // ------------------------------------------------------------------------------
    // Artillery fires through or into its own lines.
    // There is no terrain line of sight anywhere in targeting. The only own-unit test,
    // Regiment.CheckOwnUnitInWay (Regiment.cs ~9613), is a flat angle check against the units
    // in front, and lets artillery overshoot any friendly unit 10+ units away. A shell aimed over
    // a ridge or a friendly line that sits on the slope hits the ground near that line, and its
    // fragments (which have no alliance check, Projectile.GetExplosivesHits) hit friendly blocks.
    // Friendly fire stays on. Fix: when an artillery gun picks a target (FireBullet -> this
    // check, once per candidate enemy), trace the shot's arc the way Projectile does
    // (sine arc, peak = weapon.trajectorypeak / firerange x distance / battlefieldrealdimensionfactor)
    // and treat the target as blocked if
    //   (a) the arc passes through a friendly company block within NearDistance of the gun, or
    //   (b) the arc meets the ground short of the target with a friendly block within fragment
    //       range of that point.
    // A blocked target is skipped exactly like vanilla's own-unit-in-way case, so the gun picks
    // another target or holds fire.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "CheckOwnUnitInWay")]
    static class ArtilleryLineOfFire_Patch
    {
        static readonly Collider[] Buffer = new Collider[64];

        static bool IsFriendlyBlock(Collider c, Regiment self)
        {
            if (c == null || c.gameObject.tag != "RegBlock") return false;
            var coy = c.GetComponent<Coy>();
            var r = coy != null ? coy.assignedparentreg : null;
            return r != null && r != self && r.alliance == self.alliance && r.unittyp != 5 && r.unittyp <= 13;
        }

        static bool FriendlyNear(Vector3 p, float radius, Regiment self)
        {
            int n = Physics.OverlapSphereNonAlloc(p, radius, Buffer);
            for (int i = 0; i < n; i++)
                if (IsFriendlyBlock(Buffer[i], self)) return true;
            return false;
        }

        static void Postfix(Regiment __instance, Vector3 spriteposition, Vector3[] enemyoutterposition,
                            ref float distancedetected, ref bool __result)
        {
            if (__result || !Cfg.ArtilleryLineOfFire.Value) return;
            var r = __instance;
            if (r.unittyp != 2 || enemyoutterposition == null || enemyoutterposition.Length == 0) return;
            if (r.weapon < 0 || r.weapon >= GameVars.weapon.Length) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            try
            {
                var w = GameVars.weapon[r.weapon];
                Vector3 target = Vector3.zero;
                foreach (var v in enemyoutterposition) target += v;
                target /= enemyoutterposition.Length;

                Vector3 start = spriteposition + Vector3.up * 2f;
                Vector3 end = target + Vector3.up * 2.5f;
                float dist = Tools.GetXZDistance(start, end);
                if (dist < 1f) return;
                float peak = w.trajectorypeak / Mathf.Max(1f, r.firerange) * dist / Mathf.Max(0.01f, GamePrefs.battlefieldrealdimensionfactor);
                float near = Cfg.ArtilleryNearDistance.Value;
                float fragRange = Mathf.Max(2f, w.projectileweight_kg * GamePrefs.projectileweightratiorange);
                float targetClearance = Mathf.Max(5f, fragRange);   // landing this close to the target is a normal shot

                const float step = 1f;
                Vector3 prev = start;
                for (float d = step; d <= dist; d += step)
                {
                    float t = d / dist;
                    Vector3 p = Vector3.Lerp(start, end, t);
                    p.y = Mathf.Lerp(start.y, end.y, t) + Mathf.Sin(t * Mathf.PI) * peak;

                    // (b) arc meets the ground before the target
                    float ground = terrain.SampleHeight(p) + terrain.transform.position.y;
                    if (p.y <= ground && dist - d > targetClearance)
                    {
                        p.y = ground;
                        if (FriendlyNear(p, fragRange, r))
                        {
                            distancedetected = d;
                            __result = true;
                        }
                        return;   // the shell stops here either way
                    }

                    // (a) arc passes through a friendly block close to the gun
                    if (d <= near)
                    {
                        var hits = Physics.RaycastAll(prev, p - prev, Vector3.Distance(prev, p));
                        foreach (var h in hits)
                            if (IsFriendlyBlock(h.collider, r))
                            {
                                distancedetected = d;
                                __result = true;
                                return;
                            }
                    }
                    prev = p;
                }
            }
            catch (System.Exception e)
            {
                BugFixPlugin.Log.LogWarning("Artillery line of fire: " + e.Message);
            }
        }
    }

    // ------------------------------------------------------------------------------
    // Attack moves wipe combat behaviour.
    // SetMovementMode(3) sets combatbehavior = 0 for infantry and cavalry (Regiment.cs ~15828).
    // Mode 3 is entered by the Assault order, mounted Advance, an infantry right-click with
    // Charge on, and at the end of every tracked charge. So cavalry leave "charge" after their
    // first charge and infantry leave "engage" (3) as soon as they move, which also makes the
    // fire-one-volley-then-charge logic unreachable. Fix: keep behaviours 3 and 6 across the call.
    // Range behaviours 0-2 are still reset to close range, as vanilla intends.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.SetMovementMode))]
    static class KeepCombatBehaviour_Patch
    {
        struct State { public bool keep; public int cb, cbOrdered; }

        static void Prefix(Regiment __instance, int newmode, out State __state)
        {
            __state = default;
            if (!Cfg.KeepCombatBehaviourOnAttackMove.Value) return;
            var r = __instance;
            if (newmode == 3 && (r.unittyp == 0 || r.unittyp == 1) && (r.combatbehavior == 3 || r.combatbehavior == 6))
                __state = new State { keep = true, cb = r.combatbehavior, cbOrdered = r.combatbehaviorordered };
        }

        static void Postfix(Regiment __instance, State __state)
        {
            if (!__state.keep) return;
            __instance.combatbehavior = __state.cb;
            __instance.combatbehaviorordered = __state.cbOrdered;
        }
    }

    // ------------------------------------------------------------------------------
    // Cavalry Charge-off / Evade-off buttons do nothing.
    // Both send behaviour 5 (PanelHandler.cs ~3146/3198), which SetCombatBehavior rejects for
    // every non-artillery unit (Regiment.cs ~13212). The button changes state but the unit
    // stays in 3/4/6. Fix: map 5 to 0 (fire normally) for cavalry.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.SetCombatBehavior))]
    static class CavalryButtonOff_Patch
    {
        static void Prefix(Regiment __instance, ref int newbehavior)
        {
            if (Cfg.CavalryButtonOff.Value && __instance.unittyp == 1 && newbehavior == 5)
                newbehavior = 0;
        }
    }

    // ------------------------------------------------------------------------------
    // Skirmisher / evading cavalry fallback jitter.
    // CheckFallbackDueToEnemyClose puts the fallback point 31 m from the enemy on the line from
    // the enemy towards the parent brigade (Regiment.cs ~20037). The unit's own position is not
    // used. When the enemy is between the unit and its parent, or off to one side, that point
    // is on the far side of the enemy, the unit never gets clear, and it re-paths and re-faces
    // every time the enemy moves 5 m. Fix: in that geometry use the same construction but on
    // the line from the enemy through the unit itself, i.e. straight away from the enemy.
    // Normal geometry is left to vanilla.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "CheckFallbackDueToEnemyClose")]
    static class SkirmisherFallback_Patch
    {
        static bool Prefix(Regiment __instance)
        {
            if (!Cfg.SkirmisherFallback.Value) return true;
            var r = __instance;
            // Same gate as vanilla.
            if ((r.unittyp <= 2 && r.ai_stance != 1 && r.combatbehavior != 4) || r.isrouted || r.reattachmentorder
                || r.unittyp == 5 || (r.unittyp == 1 && r.mounted <= 0 && r.combatbehavior == 4) || r.unittyp > 13)
                return true;
            if (r.parentregiment == null || r.inmelee > 0f) return true;

            GameObject enemy = r.GetClosestEnemyUnit(excludegroups: true);
            if (enemy == null) return true;
            Vector3 self = r.GetPosition(), foe = enemy.transform.position, parent = r.parentregiment.transform.position;
            float dSelfEnemy = Tools.GetXZDistance(self, foe);
            if (dSelfEnemy > GamePrefs.minimumdistancetonextenemyunittofallback) return true;

            // Where vanilla would send us: 31 m from the enemy towards the parent.
            Vector3 toParent = new Vector3(parent.x - foe.x, 0f, parent.z - foe.z).normalized;
            float d = GamePrefs.minimumdistancetonextenemyunittofallback + GamePrefs.triggertorenewfallback;
            Vector3 vanillaTarget = foe + toParent * d;
            Vector3 away = new Vector3(self.x - foe.x, 0f, self.z - foe.z);
            bool targetIsAway = Vector3.Dot(new Vector3(vanillaTarget.x - self.x, 0f, vanillaTarget.z - self.z), away) > 0f;
            bool enemyCloserToParent = Tools.GetXZDistance(foe, parent) < Tools.GetXZDistance(self, parent);
            if (targetIsAway && !enemyCloserToParent) return true;   // normal geometry: vanilla is fine

            var bfs = RegimentAccess.Bfs(r);
            if (bfs == null) return true;
            float angle = bfs.GetAngleTerrain(foe, self) + 180f;    // same convention vanilla uses with the parent
            Vector3 val = bfs.GetAngleTerrainPos(foe, angle, d, RegimentAccess.Terrain(r));

            if (Tools.GetXZDistance(r.GetLastWaypointPos(), val) < GamePrefs.triggertorenewfallback)
                return false;   // already heading there
            float rot = -1f;
            if (r.movementmode != 5)
            {
                r.SetMovementMode(2);
                rot = bfs.GetAngleTerrain(self, val);
            }
            r.unitlinkedto = null;
            r.immediateunitplacement = false;
            r.RegimentSetPath(val, newpath: true, skipcover: true, rot, modifylastwaypoint: false, 2, -1f, null,
                              autoadvancefallback: true, uimovement: false);
            return false;
        }
    }

    // ------------------------------------------------------------------------------
    // Routed detachments run straight through enemies to their parent.
    // A rout first plots an enemy-avoiding path (Retreat.RetreatOrder turns on enemy
    // NavMeshObstacles), but routed units with a parent are immediately given a reattach
    // order. CheckForReattachmentOrder then paths straight at the parent with a plain NavMesh
    // path (Regiment.cs ~20629). Fix: for routed units, send that move through a
    // Retreat.RetreatOrder (the mechanism couriers use), which carves enemy obstacles before
    // pathing. Running back to the parent is kept – that part looks intended.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "CheckForReattachmentOrder")]
    static class RoutedReattach_Patch
    {
        [System.ThreadStatic] public static Regiment Active;
        static void Prefix(Regiment __instance) => Active = __instance;
        static void Finalizer() => Active = null;
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.RegimentSetPath))]
    static class RegimentSetPath_Patch
    {
        // Routed reattach redirect (see above).
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Regiment __instance, Vector3 target)
        {
            if (!Cfg.RoutedReattachAvoidsEnemy.Value) return true;
            if (RoutedReattach_Patch.Active != __instance || !__instance.isrouted) return true;
            if (Retreat.RetreatOrder.IsAlreadyRetreating(__instance)) return false;
            new Retreat.RetreatOrder(__instance, target, _usedforregularmovement: true);
            return false;
        }

        // ------------------------------------------------------------------------------
        // Column back-step.
        // The regiment transform is snapped to block 0 (AdjustRegimentParentPositions), and in
        // column block 0 sits one row behind the head (ChangeRegimentFormation). A new path starts
        // at that position and its first corner's block slots are laid out behind it, so every
        // block's first target is one row (~2.5 units, ~16 m) behind where it stands. Blocks must
        // reach a corner before moving on, so the whole column marches back, turns and then
        // advances. Fix: on a fresh path, blocks still aiming at corner 0 skip to corner 1.
        // ------------------------------------------------------------------------------
        static void Postfix(Regiment __instance, bool newpath, bool modifylastwaypoint, bool __runOriginal)
        {
            if (!__runOriginal || !Cfg.ColumnBackStep.Value || !newpath || modifylastwaypoint) return;
            var r = __instance;
            if (r.regimentpaths != 1 || r.regimentpath == null || r.regimentpath[0] == null) return;
            if (r.regimentpath[0].corners.Length < 2) return;
            if (r.blockobjectcurrentpath == null || r.blockobjectcurrentcorner == null) return;
            int n = Mathf.Min(r.blockobjects, r.blockobjectcurrentpath.Length);
            for (int k = 0; k < n; k++)
                if (r.blockobjectcurrentpath[k] == 0 && r.blockobjectcurrentcorner[k] == 0)
                    r.blockobjectcurrentcorner[k] = 1;
        }
    }

    // ------------------------------------------------------------------------------
    // Charge end keeps only the last waypoint.
    // When a tracked charge reaches its target, UpdateChargeTarget restores the unit's previous
    // waypoints by calling RegimentSetPath(newpath: true) for each one (Regiment.cs ~13383).
    // Each call wipes the path before it, so only the last waypoint survives. Fix: handle that
    // exact case first and append the waypoints (newpath only for the first).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "UpdateChargeTarget")]
    static class ChargeEndRestore_Patch
    {
        static void Prefix(Regiment __instance)
        {
            if (!Cfg.ChargeEndRestoresAllWaypoints.Value) return;
            var r = __instance;
            if (r.unittyp == 2 || r.unittyp == 5 || r.unittyp > 13) return;
            if (r.movementmode != 6 || r.chargetarget == null || r.regimentpaths > 0) return;
            if (!r.chargetarget.activeInHierarchy) return;
            var target = r.chargetarget.GetComponent<Regiment>();
            if (target == null || target.isrouted) return;   // vanilla's routed-target branch handles these
            if (Tools.GetXZDistance(r.GetLastWaypointPos(), r.chargetarget.transform.position) > 5f) return;   // vanilla re-paths

            var points = new List<Vector3>(r.oldpathpoints);
            float rot = r.oldrotation;
            r.StopRegiment(skipfinalrotation: true, manualstop: true);
            for (int j = 0; j < points.Count; j++)
                r.RegimentSetPath(points[j], newpath: j == 0, skipcover: false, rot, modifylastwaypoint: false, 2, -1f, null,
                                  autoadvancefallback: true, uimovement: false);
            r.chargetarget = null;   // vanilla's own restore block now sees no target and skips
            r.SetMovementMode(3);
        }
    }
}

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // HQs crawl when moved on their own.
    // Every unit caps its speed at its order-giver's "group speed" (Regiment.cs ~24983:
    // speed *= min(groupspeed, 1)). UpdateGroupSpeed (~31046) sets that to the slowest unit in
    // the order-giver's whole group whenever the order-giver is an HQ – including when the HQ
    // was moved alone and none of its units are moving. So an HQ (base speed 2) moved by itself
    // drops to half speed whenever its group contains unlimbered foot artillery (0.535), and the
    // same applies to any unit of a group that isn't part of the current move.
    // Fix: count only units that are moving under that same order. If none are, there is no cap.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "UpdateGroupSpeed")]
    static class GroupSpeedOnlyMovingUnits_Patch
    {
        class Cache { public int frame; public float speed; }
        static readonly Dictionary<Regiment, Cache> PerGiver = new Dictionary<Regiment, Cache>();

        static void Postfix(Regiment __instance)
        {
            if (!Cfg.HqSpeedFix.Value) return;
            var r = __instance;
            var giver = r.unitwhogavelastmovingorder;
            if (r.calledfromcampaign || r.regimentpaths <= 0 || giver == null || giver.unittyp <= 13) return;

            int frame = Time.frameCount;
            if (!PerGiver.TryGetValue(giver, out var c) || c.frame != frame)
            {
                c = c ?? new Cache();
                c.frame = frame;
                c.speed = Slowest(giver);
                PerGiver[giver] = c;
            }
            r.groupspeed = c.speed;
        }

        // Vanilla's BattleUnits.GetSlowestSpeedOfGroup, restricted to units moving under this order.
        static float Slowest(Regiment giver)
        {
            float slowest = 5f;
            bool any = false;
            var units = giver.allattachedunits;
            if (units == null) return 0f;
            foreach (var u in units)
            {
                if (u == null || u == giver || u.unittyp > 13 || u.isrouted || u.permanentlydetached) continue;
                if (!u.gameObject.activeInHierarchy || u.regimentpaths <= 0 || u.unitwhogavelastmovingorder != giver) continue;
                float f = (u.unittyp == 2 && u.ishorseartillery) ? GamePrefs.horseartilleryspeedfactor : 1f;
                int m = Mathf.Clamp(u.mounted, 0, 1);
                float s = GamePrefs.unitbasicspeed[u.unittyp, m] * f;
                if (s < slowest) slowest = s;
                any = true;
            }
            return any ? slowest : 0f;   // 0 = no cap (vanilla's convention)
        }
    }
}

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // Historical/custom battles: some player regiments ignore every group order.
    // Battle files can list single regiments with their own start position. These go into
    // BattleUnits.initialregimentoverride, and AllocateUnitsAndObjectivesToAI (BattleUnits.cs
    // ~7566) then sets permanentlydetached on every such player regiment, unconditionally.
    // Group orders (SetGroupFormation, group moves) skip detached units silently, so those
    // regiments never follow their brigade, from the first order on. Different battle files list
    // different regiments. Campaign battles always write the group flag, so they are not affected.
    // Fix: re-attach such player regiments when their parent is an HQ and they are not under
    // AI control.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.AllocateUnitsAndObjectivesToAI))]
    static class CustomBattleDetached_Patch
    {
        static readonly AccessTools.FieldRef<BattleUnits, List<BattleUnits.InitialRegimentOverride>> List =
            AccessTools.FieldRefAccess<BattleUnits, List<BattleUnits.InitialRegimentOverride>>("initialregimentoverride");

        static void Postfix(BattleUnits __instance)
        {
            if (!Cfg.CustomBattleReattach.Value || GameVars.ai_vs_ai || GameVars.debug_allarmiesai) return;
            var list = List(__instance);
            if (list == null) return;
            int n = 0;
            foreach (var o in list)
            {
                var r = o?.regimentref != null ? o.regimentref.GetComponent<Regiment>() : null;
                if (r == null || r.alliance != GameVars.playeralliance || !r.permanentlydetached) continue;
                if (r.aiassignedref != null) continue;
                var parent = r.transform.parent != null ? r.transform.parent.GetComponent<Regiment>() : null;
                if (parent == null || parent.unittyp <= 13) continue;
                r.permanentlydetached = false;
                n++;
            }
            if (n > 0) BugFixPlugin.Log.LogInfo("Re-attached " + n + " player regiments detached by the battle file");
        }
    }
}
