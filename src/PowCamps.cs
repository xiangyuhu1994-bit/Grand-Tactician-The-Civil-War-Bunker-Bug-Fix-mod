// POW camps: where prisoners go, parole, raids. Write-up: notes/pow_camps.md

using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class PowCamp
    {
        public static int Capacity(CBuilding c)
        {
            var cap = GamePrefs.powcampcapacity;
            if (cap == null || cap.Length == 0) return 0;
            return cap[Mathf.Clamp(c.level, 0, cap.Length - 1)];
        }

        public static int Held(CBuilding c) => c.prisoners == null ? 0 : Tools.SumUp(c.prisoners);

        // "Loyal": the camp stands in a state that belongs to its owner (not captured territory).
        public static bool Loyal(CBuilding c) =>
            c.state < 0 || GameVars.nation == null || c.state >= GameVars.nation.Length || GameVars.nation[c.state].alliance == c.Owner;

        // Nearest finished camp of `owner`, by tier: loyal with room, captured with room,
        // loyal full, captured full. `exclude` is skipped. requireRoom limits to the first two tiers.
        public static CBuilding Pick(Vector3 pos, int owner, CBuilding exclude, bool requireRoom)
        {
            CBuilding best = null; int bestTier = int.MaxValue; float bestDist = float.MaxValue;
            foreach (var c in BattleUnits.powcamps)
            {
                if (c == null || c == exclude || c.Owner != owner || c.BuildingType != CBuilding.id_powcamp) continue;
                if (c.constructiontimer > 0f || c.upgradetimer > 0f) continue;
                bool room = Held(c) < Capacity(c);
                if (requireRoom && !room) continue;
                int tier = (room ? 0 : 2) + (Loyal(c) ? 0 : 1);
                float d = Vector3.Distance(c.transform.position, pos);
                if (tier < bestTier || (tier == bestTier && d < bestDist)) { best = c; bestTier = tier; bestDist = d; }
            }
            return best;
        }
    }

    // ------------------------------------------------------------------------------
    // Released prisoners shrink their own side's recruiting pool.
    // CBuilding.PardonPrisoners(number, alliance) (~2769) is used for parole, for prisoners freed
    // when a camp is captured, and (in this mod) for raids. It calls
    // Nation.AdjustRecruitingPool(+amount), but that function counts men *recruited*: available
    // volunteers = potential - volunteersrecruited (GameVars ~4879), and towns lose 5 workers per
    // recruited man. RecruitUnit uses +strength and disbanding uses -strength. So every
    // released prisoner cost his own side recruits and workforce instead of adding them.
    // Fix: same distribution and re-enlistment ratio, with the sign reversed (never taking a
    // state's recruited count below 0).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(CBuilding), nameof(CBuilding.PardonPrisoners), new[] { typeof(int), typeof(int) })]
    static class PardonSign_Patch
    {
        static bool Prefix(int number, int alliance)
        {
            if (!Cfg.PardonAddsRecruits.Value) return true;
            if (alliance > 1 || number <= 0 || GameVars.nation == null) return false;
            float total = 0f;
            foreach (var n in GameVars.nation) total += n.volunteersrecruited[alliance];
            foreach (var n in GameVars.nation)
            {
                float ratio = n.GetReenlistmentRatio(alliance);
                int amount = Mathf.FloorToInt(number * n.volunteersrecruited[alliance] / Mathf.Max(1f, total) * ratio);
                amount = Mathf.Min(amount, Mathf.Max(0, n.volunteersrecruited[alliance]));
                if (amount > 0) n.AdjustRecruitingPool(-amount, 0, alliance);
            }
            return false;
        }
    }

    // ------------------------------------------------------------------------------
    // New prisoners go to the nearest camp, full or not, including one just captured.
    // WriteBattleResultToMonument (ImportExportUnitData.cs ~3168) sends each beaten group's
    // prisoners to the captor's nearest finished camp anywhere, ignoring capacity. A camp taken
    // in enemy territory is usually the nearest, so it fills first and is lost (with everyone
    // in it released) if the enemy retakes the ground. Fix: prefer camps with room in states
    // loyal to the owner, then captured camps with room, then loyal full camps, then any.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.WriteBattleResultToMonument))]
    static class PowBattleResultScope_Patch
    {
        [System.ThreadStatic] public static bool Active;
        static void Prefix() => Active = true;
        static void Finalizer() => Active = false;
    }

    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.GetClosestBuilding))]
    static class PowCampChoice_Patch
    {
        static bool Prefix(Vector3 position, int buildingtype, int owner, ref CBuilding __result)
        {
            if (!PowBattleResultScope_Patch.Active || buildingtype != CBuilding.id_powcamp || !Cfg.PowCampPriority.Value) return true;
            if (owner < 0) { __result = null; return false; }
            __result = PowCamp.Pick(position, owner, null, requireRoom: false);
            return false;
        }
    }

    // Overflow transfers: same preference, room required (as vanilla).
    [HarmonyPatch(typeof(CBuilding), nameof(CBuilding.CheckPrisonersTransfers))]
    static class PowTransfer_Patch
    {
        static bool Prefix(CBuilding __instance)
        {
            if (!Cfg.PowCampPriority.Value) return true;
            var c = __instance;
            int excess = PowCamp.Held(c) - PowCamp.Capacity(c);
            if (excess <= 0 || c.prisoners == null) return false;
            var target = PowCamp.Pick(c.transform.position, c.Owner, c, requireRoom: true);
            if (target == null) return false;
            int move = Mathf.Max(0, Mathf.Min(PowCamp.Capacity(target) - PowCamp.Held(target), excess));
            for (int j = 0; j < c.prisoners.Length && move > 0; j++)
            {
                int n = Mathf.Min(c.prisoners[j], move);
                c.prisoners[j] -= n;
                target.prisoners[j] += n;
                move -= n;
            }
            return false;
        }
    }

    // ------------------------------------------------------------------------------
    // The Parole button empties the whole camp.
    // PanelHandler (~5613) calls CBuilding.PardonPrisoners(), the same routine that frees
    // everyone when a camp is captured. Fix: when the camp is over capacity, the button paroles
    // only the excess, spread over the prisoners' alliances in proportion. Within capacity it
    // paroles everyone, as before. Capture still frees everyone.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(CBuilding), nameof(CBuilding.ChangeOwnership))]
    static class PowCaptureScope_Patch
    {
        [System.ThreadStatic] public static bool Active;
        static void Prefix() => Active = true;
        static void Finalizer() => Active = false;
    }

    [HarmonyPatch(typeof(CBuilding), nameof(CBuilding.PardonPrisoners), new System.Type[0])]
    static class ParoleExcess_Patch
    {
        static bool Prefix(CBuilding __instance)
        {
            if (!Cfg.ParoleExcessOnly.Value || PowCaptureScope_Patch.Active) return true;
            var c = __instance;
            if (c.prisoners == null) return true;
            int held = PowCamp.Held(c), excess = held - PowCamp.Capacity(c);
            if (excess <= 0 || held <= 0) return true;
            int left = excess;
            for (int i = 0; i < c.prisoners.Length && left > 0; i++)
            {
                int n = Mathf.Min(c.prisoners[i], Mathf.CeilToInt(c.prisoners[i] * (float)excess / held));
                n = Mathf.Min(n, left);
                if (n <= 0) continue;
                CBuilding.PardonPrisoners(n, i);
                c.prisoners[i] -= n;
                left -= n;
            }
            return false;
        }
    }

    // ------------------------------------------------------------------------------
    // Feature: raids free prisoners.
    // A raid (Regiment.UpdateRaids ~4750) only lowers a building's condition; a raided POW camp
    // loses nothing. Fix: each time a raid damages an enemy POW camp, the share of prisoners
    // freed equals the condition lost x RaidPowFreeShare (default: half of them by the time the
    // camp is at condition 0). Guards slow raids down, so a guarded camp resists. Freed men go
    // to their side's recruiting pools exactly like paroled men. CBuilding.Damage on a POW camp
    // comes only from raids (Autocalc damages fort garrisons only).
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(CBuilding), nameof(CBuilding.Damage))]
    static class RaidFreesPows_Patch
    {
        static void Prefix(CBuilding __instance, out float __state) => __state = __instance.Condition;

        static void Postfix(CBuilding __instance, float __state)
        {
            var c = __instance;
            if (!Cfg.RaidFreesPows.Value || c.BuildingType != CBuilding.id_powcamp || c.prisoners == null) return;
            float lost = __state - c.Condition;
            if (lost <= 0f) return;
            float share = Mathf.Clamp01(lost * Cfg.RaidPowFreeShare.Value);
            int freed = 0;
            for (int i = 0; i < c.prisoners.Length; i++)
            {
                if (c.prisoners[i] <= 0 || i == c.Owner || !GameVars.alliance[c.Owner].IsAtWarWith(i)) continue;
                int n = Mathf.Min(c.prisoners[i], Mathf.CeilToInt(c.prisoners[i] * share));
                if (n <= 0) continue;
                CBuilding.PardonPrisoners(n, i);
                c.prisoners[i] -= n;
                freed += n;
            }
            if (freed > 0)
            {
                RollingText.Add(c.transform.position, freed + " prisoners freed", RollingText.type_raid, c.gameObject, showsymbol: true, "", "", 10f);
                GameVars.DebugOwnLog("bugFixMod: raid freed " + freed + " prisoners from " + c.BuildingName);
            }
        }
    }

    // ------------------------------------------------------------------------------
    // Raid effects snowball.
    // UpdateRaids (Regiment.cs ~4698) sets lastraidupdate only once, when it is 0, and never
    // advances it. Every update then applies loot, enemy stock losses, trade-warfare costs and
    // state support changes for the whole time since the raid began, not since the last
    // update, so a raid's effects grow every tick (until a save/load resets the private timer).
    // Fix: after each update the timer moves to now; it is reset when the raid order ends, so
    // a new raid starts fresh.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), "UpdateRaids")]
    static class RaidTimer_Patch
    {
        static readonly AccessTools.FieldRef<Regiment, float> LastRaidUpdate = AccessTools.FieldRefAccess<Regiment, float>("lastraidupdate");

        static void Postfix(Regiment __instance)
        {
            if (!Cfg.RaidTimerFix.Value) return;
            LastRaidUpdate(__instance) = __instance.cavalryorders == 2 ? GameVars.GetCampaignFloatingDate() : 0f;
        }
    }
}
