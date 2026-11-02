// Campaign fixes: empty HQ battle rules, fleet bombardment of besieged forts, level-3 forts.
// Write-up: notes/campaign.md

using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.EstablishCampaignBattle))]
    static class EstablishCampaignBattle_Patch
    {
        const int ArmyHQ = 18;
    
        static readonly AccessTools.FieldRef<BattlefieldSetup> Bfs =
            AccessTools.StaticFieldRefAccess<BattlefieldSetup>(AccessTools.Field(typeof(BattleUnits), "bfs"));
    
        // Troops physically present: a fighting unit's own strength, or for an HQ the fighting
        // units under it that are within `range` of it. An HQ's groupstrength counts every
        // subordinate wherever it is, so an HQ that marched ahead of its divisions looked
        // "full" to the old test and could still open a phantom siege.
        internal static int LocalStrength(Regiment r, float range)
        {
            if (r == null) return 0;
            if (r.unittyp <= 13) return r.strength > 0 ? r.strength : 0;
            if (r.unittyp == 17) return 0;
            int sum = 0;
            var units = r.allattachedunits;
            if (units == null) return 0;
            Vector3 p = r.transform.position;
            foreach (var u in units)
            {
                if (u == null || u == r || u.unittyp > 13 || u.strength <= 0 || u.onretreat) continue;
                if (Tools.GetXZDistance(u.transform.position, p) <= range) sum += u.strength;
            }
            return sum;
        }

        static float Range(Regiment r) => Mathf.Max(r.buglerange, 1f);

        // An HQ with no troops on the spot. Garrisons, fleets and units already fighting are ignored.
        internal static bool IsEmptyHQ(Regiment r)
        {
            if (r == null || r.unittyp <= 13 || r.unittyp == 17 || r.garrisonreference != null || r.inbattle)
                return false;
            return LocalStrength(r, Range(r)) <= 0;
        }

        // An HQ that has no troops left at all, anywhere.
        static bool HasNoUnitsAnywhere(Regiment r)
        {
            r.UpdateGroupStrength(overridecalc: true, ignoreactivestate: true);
            return r.groupstrength <= 0;   // groupstrengthactive is forced to 1 for empty HQs; use the raw sum
        }

        // Same tests vanilla's CheckBattleParticipation uses to pull a friendly unit into the fight,
        // plus "has troops on the spot". Range: bugle range for sieges, command range for field battles.
        static bool HasSupport(BattleUnits bu, Regiment hq, bool siege)
        {
            float range = siege ? hq.buglerange : hq.commanderrange;
            bool water = hq.currentterrain == 3 || hq.currentterrain == 4 || hq.currentterrain == 8;
            int[] terrain = water ? GameVars.terrainrestrictions_watermovementcommand : GameVars.terrainrestrictions_command;

            foreach (var r in hq.unitrange.owninrangeregs)
            {
                if (r == null || r == hq || r.unittyp == 17 || r.IsFortGarrison() || r.onretreat || r.inbattle)
                    continue;
                if (Tools.GetXZDistance(r.transform.position, hq.transform.position) > range)
                    continue;
                if (LocalStrength(r, Range(r)) <= 0)
                    continue;
                r.UpdateGroupStrength(overridecalc: true, ignoreactivestate: true);
                if (r.groupmorale < GamePrefs.minimummoraleforunitstoparticipateinbattles)
                    continue;
                if (bu.CheckIfUnitIsWithinModifiedRange(hq, r.transform.position, range, terrain))
                    return true;
            }
            return false;
        }

        static void Withdraw(Regiment r) =>
            r.SetRetreatToDepot(r.transform.position, GamePrefs.retreatingdistancefactor, GamePrefs.maxretreatingdistancefactor);
    
        // Returning false skips the original; __result is what the caller sees.
        // Caller (Regiment.cs ~6719): true = the calling unit withdraws.
        static bool Prefix(BattleUnits __instance, Regiment callingunit, Regiment foundenemy, ref bool __result)
        {
            if (callingunit == null || foundenemy == null)
                return true;
            bool hqRules = Cfg.EmptyHQRules.Value, fleetFix = Cfg.FleetBombardBesiegedFort.Value;
            if (!hqRules && !fleetFix)
                return true;
            if (!GameVars.alliance[callingunit.alliance].IsAtWarWith(foundenemy.alliance))
                return true;
    
            // --- Fixes 1 and 2: empty HQs ---
            // Same siege test vanilla uses (BattleUnits.cs ~2982/2986).
            bool siege = callingunit.unittyp != 17 && foundenemy.unittyp != 17 &&
                         (callingunit.garrisonreference != null || foundenemy.garrisonreference != null ||
                          (callingunit.campaignstance == 1 && foundenemy.campaignstance == 1));
    
            if (hqRules && IsEmptyHQ(callingunit))
            {
                if (callingunit.unittyp != ArmyHQ && HasNoUnitsAnywhere(callingunit))
                {
                    __result = true;                    // corps/division/brigade HQ with no units left: caller withdraws it
                    return false;
                }
                if (!HasSupport(__instance, callingunit, siege))
                {
                    __result = false;                   // lone HQ: never starts a fight on its own
                    return false;
                }
            }
            if (hqRules && IsEmptyHQ(foundenemy))
            {
                bool callerHasTroops = callingunit.unittyp == 17 || !IsEmptyHQ(callingunit) ||
                                       callingunit.garrisonreference != null;
                if (foundenemy.unittyp != ArmyHQ && HasNoUnitsAnywhere(foundenemy))
                {
                    Withdraw(foundenemy);
                    __result = false;
                    return false;
                }
                if (!HasSupport(__instance, foundenemy, siege))
                {
                    if (callerHasTroops) Withdraw(foundenemy);
                    __result = false;
                    return false;
                }
            }
    
            // --- Fix 3: fleet vs. a garrison that is already in a siege ---
            // Vanilla hits the "join existing battle" branch first (only fleets may join
            // fleets) and returns before reaching the bombardment code.
            if (fleetFix && callingunit.unittyp == 17 && !callingunit.inbattle && foundenemy.unittyp != 17
                && foundenemy.inbattle && foundenemy.IsFortGarrison()
                && !foundenemy.onretreat && !callingunit.onretreat
                && callingunit.fortbombardmentengine == null
                && callingunit.fleetref != null && callingunit.fleetref.attachedships != null
                && callingunit.fleetref.attachedships.Length > 0)
            {
                if (foundenemy.garrisonreference.Condition >= GamePrefs.strengthrelationtriggerdefeatnavybattles
                    && Tools.GetXZDistance(callingunit.GetPosition(), foundenemy.GetPosition()) <= callingunit.buglerange)
                {
                    var engine = callingunit.gameObject.AddComponent<Autocalc>();
                    if (engine.StartFortBombardment(callingunit, foundenemy) != null)
                    {
                        callingunit.fortbombardmentengine = engine;
                        var bfs = Bfs();
                        if (bfs != null && callingunit.GetLastTransmittedPath() < 0)
                            callingunit.RotateRegiment(bfs.GetAngleTerrain(callingunit.transform.position,
                                                                           foundenemy.transform.position) + 90f);
                    }
                    else
                    {
                        Object.Destroy(engine);   // vanilla leaves a dead engine attached in this case
                    }
                }
                __result = false;
                return false;
            }
    
            return true;
        }
    }

    // ------------------------------------------------------------------------------
    // Phantom sieges keep running for the whole setup period.
    // UpdateSiegeCombat (Autocalc.cs ~2167) returns early while siege works are being set up
    // (setuptimesiege[fort level], about a day), before it checks whether either side can still
    // fight. A siege opened by an HQ with no troops on the spot, or one whose only attackers have
    // left, therefore keeps draining the other side's readiness until setup ends. Fix: each
    // siege update, if one side has no fighting men left (non-withdrawn units of unittyp <= 13
    // with strength, not retreating), end the siege at once with the other side winning,
    // using the game's own FinishLandBattle. Also cleans up phantom sieges in existing saves.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Autocalc), nameof(Autocalc.UpdateSiegeCombat))]
    static class PhantomSiege_Patch
    {
        static readonly System.Reflection.MethodInfo Finish = AccessTools.Method(typeof(Autocalc), "FinishLandBattle");

        static int FightingStrength(Autocalc.EnvolvedUnits side)
        {
            if (side == null || side.singleunitlist == null) return 0;
            int sum = 0;
            for (int i = 0; i < side.singleunitlist.Count; i++)
            {
                var u = side.singleunitlist[i];
                if (u == null || u.unittyp > 13 || u.strength <= 0 || u.onretreat || u.isrouted) continue;
                if (side.withdrawn != null && i < side.withdrawn.Count && side.withdrawn[i]) continue;
                sum += u.strength;
            }
            return sum;
        }

        static bool Prefix(Autocalc __instance)
        {
            if (!Cfg.EmptyHQRules.Value || __instance.type != 2 || Finish == null) return true;
            var sides = __instance.envolvedunits;
            if (sides == null || sides.Length < 2 || __instance.alliance == null) return true;
            int s0 = FightingStrength(sides[0]), s1 = FightingStrength(sides[1]);
            if (s0 > 0 && s1 > 0) return true;
            if (s0 <= 0 && s1 <= 0) return true;   // nothing sensible to decide; leave it to vanilla
            int loser = s0 <= 0 ? 0 : 1, winner = 1 - loser;
            int endbattle = __instance.alliance[loser] == GameVars.playeralliance ? 8 : 0;   // same as vanilla's own end check
            try
            {
                BugFixPlugin.Log.LogInfo("Ending phantom siege: side " + loser + " has no fighting men");
                Finish.Invoke(__instance, new object[] { endbattle, winner });
            }
            catch (System.Exception e)
            {
                BugFixPlugin.Log.LogWarning("Phantom siege end failed: " + (e.InnerException ?? e).Message);
                return true;
            }
            return false;
        }
    }

    // ------------------------------------------------------------------------------
    // AI marches HQs with no troops into enemy territory.
    // AICampaign.MoveUnitTo (AICampaign.cs ~5770) is the AI's single move order. It is used for
    // every top unit, including HQs whose units are gone or elsewhere, so lone HQs walk into
    // enemy forts and armies. Fix: if the AI moves an HQ with no troops on the spot to a place
    // on the enemy side of the front, send it to the nearest friendly force with troops
    // instead (or don't move it at all if there is none).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(AICampaign), nameof(AICampaign.MoveUnitTo))]
    static class AiLoneHQ_Patch
    {
        static readonly AccessTools.FieldRef<BattleUnits> BUnits =
            AccessTools.StaticFieldRefAccess<BattleUnits>(AccessTools.Field(typeof(AICampaign), "bunits"));

        static bool Prefix(Regiment unit, ref Vector3 position, ref bool __result)
        {
            if (!Cfg.AiLoneHQ.Value || unit == null) return true;
            if (unit.alliance == GameVars.playeralliance && !GameVars.ai_vs_ai) return true;
            if (!EstablishCampaignBattle_Patch.IsEmptyHQ(unit)) return true;
            var bu = BUnits();
            if (bu == null || bu.frontline2 == null) return true;
            int side = bu.frontline2.GetSideOnPosition(position);
            if (side < 0 || side == unit.alliance || !GameVars.alliance[unit.alliance].IsAtWarWith(side)) return true;

            // Nearest friendly field force with troops on the spot.
            Regiment best = null;
            float bestD = float.MaxValue;
            foreach (var r in Object.FindObjectsOfType<Regiment>())
            {
                if (r == null || r == unit || r.alliance != unit.alliance || !r.istopunit || r.unittyp == 17
                    || r.garrisonreference != null || r.inbattle || r.onretreat) continue;
                if (EstablishCampaignBattle_Patch.LocalStrength(r, Mathf.Max(r.buglerange, 1f)) <= 0) continue;
                float d = Tools.GetXZDistance(r.transform.position, unit.transform.position);
                if (d < bestD) { bestD = d; best = r; }
            }
            if (best == null)
            {
                __result = false;   // stay put rather than walk into the enemy alone
                return false;
            }
            position = best.transform.position;
            return true;
        }
    }

    // ------------------------------------------------------------------------------
    // Empty HQs march at 70 % speed on the campaign map.
    // Campaign speed is scaled by group training (Regiment.cs ~24460, movementspeedtraining
    // 0.7 at training 0 to 1.2 at training 1). An HQ with no units averages nothing, so its
    // grouptraining stays 0 and it always moves at 70 %. Fix: an HQ with no units gets
    // grouptraining 0.6, which is a neutral 1.0 speed factor.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.UpdateGroupStrength))]
    static class EmptyHQTraining_Patch
    {
        static void Postfix(Regiment __instance)
        {
            if (!Cfg.HqSpeedFix.Value) return;
            var r = __instance;
            if (!r.calledfromcampaign || r.unittyp <= 13 || r.unittyp == 17 || r.groupstrength > 0) return;
            if (r.grouptraining <= 0f) r.grouptraining = 0.6f;
        }
    }

    // Level-3 forts. GameVars.weapongroupforts (fort level -> weapon group) is hardcoded
    // to { 0, 1, 2 }. Three call sites index it without clamping, so a level-3 fort throws
    // IndexOutOfRangeException inside Autocalc.Update every frame and the siege or
    // bombardment never progresses. weapongroups.dat already contains group 3
    // ("Fort Armory Level 3"), so extending the map fixes the crash and gives level-3
    // forts their own guns.
    [HarmonyPatch(typeof(GameVars), nameof(GameVars.ImportWeapons))]
    static class LevelThreeForts_Patch
    {
        static void Postfix()
        {
            if (!Cfg.LevelThreeForts.Value) return;
            int groups = GameVars.weapongroup != null ? GameVars.weapongroup.Count : 0;
            GameVars.weapongroupforts = new int[] { 0, 1, 2, groups > 3 ? 3 : 2 };
            BugFixPlugin.Log.LogInfo("Level-3 forts use weapon group " + GameVars.weapongroupforts[3]);
        }
    }
}
