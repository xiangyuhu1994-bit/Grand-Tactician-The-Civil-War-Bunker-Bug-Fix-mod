// AI weapon valuation and purchasing; career commander-in-chief path. Write-up: notes/ai_weapons_and_cic.md

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class WeaponValue
    {
        const int RefRifle = 14;   // Springfield M1855/61
        const int RefGun = 1;      // M1857 12-pounder Napoleon

        // Vanilla AICampaign.GetWeaponStrengthFactor (AICampaign.cs ~7543), for scaling.
        public static float Vanilla(int id)
        {
            var w = GameVars.weapon[id];
            float v = w.range_yd * Mathf.Pow(w.roundsperminute_pract, 0.2f) * (w.firepower[0] / 2f) * Mathf.Pow(w.accuracy, 3f);
            if (w.isari) v /= Mathf.Pow(w.tubeweight_kg, 0.2f) / 10f * 100f;
            return v / 1000f;
        }

        // Same shape as the auto-resolve weapon term (Autoresolve.cs), with raw weapon accuracy
        // because a purchase has no crew yet: sqrt(FP x (ROF/3)^r x (acc/0.9)^a x (range/ref)^g).
        public static float New(int id)
        {
            var w = GameVars.weapon[id];
            float r = Cfg.AutoresolveRofExponent.Value, a = Cfg.AutoresolveAccuracyExponent.Value, g = Cfg.AutoresolveRangeExponent.Value;
            if (!w.isari)
            {
                float fp = Mathf.Max(0.01f, w.firepower[0]);
                float rof = Mathf.Pow(Mathf.Max(0.1f, w.roundsperminute_pract) / 3f, r);
                float acc = Mathf.Pow(Mathf.Max(0.1f, w.accuracy) / 0.9f, a);
                float rng = Mathf.Pow(Mathf.Max(10f, w.range_yd) / 350f, g);
                return Mathf.Sqrt(fp * rof * acc * rng);
            }
            float sum = 0f; int n = 0;
            foreach (var f in w.firepower) if (f > 0f) { sum += f; n++; }
            float gfp = n > 0 ? sum / n : 0.01f;
            float grof = Mathf.Pow(Mathf.Clamp(w.roundsperminute_pract, 0.1f, 6f) / 3f, r);   // machine guns capped
            float gacc = Mathf.Pow(Mathf.Max(0.1f, w.accuracy), a);
            float grng = Mathf.Pow(Mathf.Max(10f, w.range_yd) / 1600f, g);
            return Mathf.Sqrt(gfp * grof * gacc * grng);
        }

        static float scaleRifle = -1f, scaleGun = -1f;
        public static float Scale(bool ari)
        {
            if (ari)
            {
                if (scaleGun < 0f) scaleGun = Ref(RefGun, true);
                return scaleGun;
            }
            if (scaleRifle < 0f) scaleRifle = Ref(RefRifle, false);
            return scaleRifle;
        }

        static float Ref(int id, bool ari)
        {
            if (GameVars.weapon == null || id >= GameVars.weapon.Length || GameVars.weapon[id].isari != ari) return 1f;
            float n = New(id);
            return n > 0f ? Vanilla(id) / n : 1f;
        }

        public static void Reset() { scaleRifle = -1f; scaleGun = -1f; }

        // > 0 while a career prestige cost is being computed: GetWeaponStrengthFactor returns vanilla.
        [System.ThreadStatic] public static int VanillaDepth;
    }

    // ------------------------------------------------------------------------------
    // Career prestige costs (weapon upgrades, raising units, command costs) keep vanilla's
    // weapon rating, which prices premium weapons well. GetBestWeapon, called from inside the
    // upgrade cost to pick the weapon, still uses the new rating.
    // ------------------------------------------------------------------------------
    [HarmonyPatch]
    static class VanillaPrestigeRating_Patch
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DLC_WL), nameof(DLC_WL.GetWeaponUpgradePrestigeCost), new[] { typeof(Regiment), typeof(int) });
            yield return AccessTools.Method(typeof(DLC_WL), nameof(DLC_WL.GetRaiseUnitCost), new[] { typeof(float), typeof(int) });
            yield return AccessTools.Method(typeof(DLC_WL), nameof(DLC_WL.GetCommandPrestigeCost), new[] { typeof(object) });
        }
        static void Prefix() => WeaponValue.VanillaDepth++;
        static void Finalizer() => WeaponValue.VanillaDepth--;
    }

    [HarmonyPatch(typeof(AICampaign), nameof(AICampaign.GetBestWeapon))]
    static class BestWeaponNewRating_Patch
    {
        static void Prefix(out int __state) { __state = WeaponValue.VanillaDepth; WeaponValue.VanillaDepth = 0; }
        static void Finalizer(int __state) => WeaponValue.VanillaDepth = __state;
    }

    // ------------------------------------------------------------------------------
    // AI weapon value overrates accuracy.
    // AICampaign.GetWeaponStrengthFactor = range x ROF^0.2 x FP/2 x accuracy^3 rates the
    // Whitworth 3.8x a Springfield, so AI purchases and AI re-arming chase Whitworths. Fix:
    // the same formula as the auto-resolve weapon term, scaled so the Springfield (rifles) and
    // the Napoleon (guns) keep their vanilla values. Career prestige costs keep the vanilla rating.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(AICampaign), nameof(AICampaign.GetWeaponStrengthFactor), new[] { typeof(int) })]
    static class WeaponValue_Patch
    {
        static void Postfix(int weaponid, ref float __result)
        {
            if (WeaponValue.VanillaDepth > 0) return;   // career prestige costs keep the vanilla rating
            if (!Cfg.AiWeaponValue.Value || GameVars.weapon == null || weaponid < 0 || weaponid >= GameVars.weapon.Length) return;
            try { __result = WeaponValue.New(weaponid) * WeaponValue.Scale(GameVars.weapon[weaponid].isari); }
            catch { /* keep vanilla */ }
        }
    }

    [HarmonyPatch(typeof(GameVars), nameof(GameVars.ImportWeapons))]
    static class WeaponValueReset_Patch
    {
        static void Postfix() => WeaponValue.Reset();
    }

    // ------------------------------------------------------------------------------
    // AI orders 27,000 of an expensive rifle at a time.
    // CheckPurchaseWeapons (AICampaign.cs ~7620) picks a random weapon rated above the side's
    // average and always orders unit size x 9 (27,000 rifles) or battery x 7 guns, never looking
    // at price, need or treasury. Fix: for rifles and guns separately, pick the weapon with the
    // largest rating gain per dollar among those better than the side's average, order only what is
    // needed to re-arm units it would improve by 10 %+ (minus stock and pending orders of at-least-
    // as-good weapons), capped by vanilla's order size. Price is not capped by the treasury:
    // running deficits and taking loans is part of the game's economy.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(AICampaign), nameof(AICampaign.CheckPurchaseWeapons))]
    static class AiPurchase_Patch
    {
        static bool Prefix(int _alliance)
        {
            if (!Cfg.AiWeaponPurchases.Value) return true;
            var al = GameVars.alliance[_alliance];
            // Same gate as vanilla.
            if ((GameVars.playeralliance == _alliance && !GameVars.automanageweapons && (!GameVars.ai_vs_ai | (GameVars.ai_vs_ai && DLC_WL.IsCommanderInChief())))
                || !al.IsRatingOkForWeaponPurchases())
                return false;
            try
            {
                Buy(_alliance, false);
                Buy(_alliance, true);
            }
            catch (System.Exception e)
            {
                BugFixPlugin.Log.LogWarning("AI weapon purchase: " + e.Message);
            }
            return false;
        }

        static float[] vals;
        static float Value(int id) => vals[id];

        static void Buy(int a, bool ari)
        {
            var al = GameVars.alliance[a];
            if (vals == null || vals.Length != GameVars.weapon.Length) vals = new float[GameVars.weapon.Length];
            for (int k = 0; k < vals.Length; k++) vals[k] = AICampaign.GetWeaponStrengthFactor(k);
            float avg = ari ? al.averageweaponstrengthguns : al.averageweaponstrengthrifles;
            int unitSize = ari ? Mathf.Max(1, Mathf.RoundToInt(GamePrefs.usedstatsgunsdummy)) : Mathf.Max(1, GamePrefs.unittypesize[0]);
            int maxOrder = ari ? (int)(GamePrefs.usedstatsgunsdummy * GamePrefs.aiweaponordersmultipleguns)
                               : (int)(GamePrefs.unittypesize[0] * GamePrefs.aiweaponordersmultiplerifles);
            int minOrder = ari ? unitSize : Mathf.Max(1, Mathf.RoundToInt(unitSize * 0.25f));

            int best = -1; float bestScore = 0f, bestPrice = 0f; int bestQty = 0;
            for (int j = 0; j < GameVars.weapon.Length; j++)
            {
                var w = GameVars.weapon[j];
                if (w.isari != ari) continue;
                if (!(w.IsResearched(a) | w.IsImported(a)) || !w.AbleToOrder(a) || al.weaponorders[j].quantity > 0) continue;
                float v = Value(j);
                if (v <= avg) continue;

                int deficit = Deficit(a, ari, j, v, out float gain);
                if (deficit < minOrder || gain <= 0f) continue;

                float price = Mathf.Abs(w.GetProductionFigures(a, j, ari ? 10f : 1000f).cost) / (ari ? 10f : 1000f);
                if (price <= 0f) continue;
                float score = gain / price;   // rating gained per man (gun) re-armed, per dollar
                if (score > bestScore) { bestScore = score; best = j; bestPrice = price; bestQty = deficit; }
            }
            if (best < 0) return;

            // No treasury cap: sides are meant to run deficits and take on loans as the war goes on.
            int qty = Mathf.Min(bestQty, maxOrder);
            if (qty < minOrder) return;

            GameVars.DebugOwnLog("bugFixMod AI order: alliance " + a + " weapon " + best + " qty " + qty + " price " + bestPrice);
            WeaponList.PlaceWeaponOrder(a, best, qty);
        }

        // Men (or guns) in units whose weapon j would improve by at least MinUpgrade (unarmed units
        // always count), minus stock and pending orders of weapons at least as good as j.
        // gain = average rating improvement per man (gun) of those units.
        const float MinUpgrade = 0.1f;
        static int Deficit(int a, bool ari, int j, float v, out float gain)
        {
            var al = GameVars.alliance[a];
            var w = GameVars.weapon[j];
            float need = 0f, gainSum = 0f;
            gain = 0f;
            if (al.unitlist != null)
                foreach (var u in al.unitlist)
                {
                    if (u == null || u.unittyp > 13) continue;
                    if (ari ? u.unittyp != 2 : (u.unittyp != 0 && u.unittyp != 1)) continue;
                    if (w.restriction == null || !w.restriction.Contains(u.unittyp)) continue;
                    if (ari && u.ishorseartillery && !w.horseari) continue;
                    float cur = u.weapon >= 0 && u.weapon < GameVars.weapon.Length ? Value(u.weapon) : 0f;
                    if (v < cur * (1f + MinUpgrade)) continue;
                    float men = ari ? u.statsguns : (u.strength + u.losses[0] + u.sick);
                    if (men <= 0f) continue;
                    need += men;
                    gainSum += (v - cur) * men;
                }
            if (need <= 0f) return 0;
            gain = gainSum / need;
            for (int k = 0; k < GameVars.weapon.Length; k++)
            {
                if (GameVars.weapon[k].isari != ari || Value(k) < v) continue;
                need -= al.weaponstock[k];
                var o = al.weaponorders[k];
                if (o.quantity > 0) need -= o.quantity - o.alreadydelivered;
            }
            return Mathf.Max(0, Mathf.FloorToInt(need));
        }
    }

    // ------------------------------------------------------------------------------
    // Career mode: the player can never become commander-in-chief.
    // UpdateCommanderInChief (BattleUnits.cs ~3581) re-picks the CiC every quarter-year by
    // campaignranking = command level x 100000 + rank x 10000 + exp x 1000 + fame x 100.
    // GetCommandLevel scores the sitting CiC as 18 (army group) wherever he serves (~6675),
    // while an army commander scores 16, so the incumbent can't be overtaken; and the player's
    // own rank is capped at his immediate superior's (the CiC when he commands a top-level army).
    // Fix: while the player commands a top-level army and is at least the CiC's rank, he is
    // scored at level 18 too for that re-pick, so rank, experience and fame decide. The game's
    // own "becoming commanding general" events then fire. The Lt. General policy freeze stays.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.UpdateCommanderInChief))]
    static class CareerCic_Patch
    {
        static void Prefix(out int __state)
        {
            __state = int.MinValue;
            if (!Cfg.CareerCicPath.Value || !DLC_WL.dlc_scenarioactive) return;
            int pid = DLC_WL.dlc_chosencommander;
            if (pid < 0 || pid >= GameVars.commander.Count) return;
            var p = GameVars.commander[pid];
            var al = GameVars.alliance[p.alliance];
            int cid = al.commanderinchief;
            if (cid < 0 || cid >= GameVars.commander.Count || cid == pid || al.ltgenpolicy) return;
            if (!p.IsAvailable()) return;
            var cmd = p.currentcommand;
            if (cmd == null || cmd.unittyp < 16 || cmd.unittyp == 17) return;
            var parent = cmd.transform.parent != null ? cmd.transform.parent.GetComponent<Regiment>() : null;
            if (parent != null) return;   // top-level army only
            var cic = GameVars.commander[cid];
            if (p.rank < cic.rank) return;

            __state = p.campaignranking;
            p.campaignranking = 18 * 100000 + p.rank * 10000 + (int)(p.exp * 1000f) + (int)(p.fame * 100f);
        }

        static void Finalizer(int __state)
        {
            if (__state == int.MinValue) return;
            int pid = DLC_WL.dlc_chosencommander;
            if (pid >= 0 && pid < GameVars.commander.Count)
            {
                var p = GameVars.commander[pid];
                // If the player became CiC, his level is 18 from now on anyway; otherwise restore.
                if (GameVars.alliance[p.alliance].commanderinchief != pid) p.campaignranking = __state;
            }
        }
    }
}
