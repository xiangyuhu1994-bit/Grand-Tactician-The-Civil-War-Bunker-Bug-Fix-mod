// Artillery features: ammunition for captured guns, per-gun ammunition, gun destruction by solid shot.
// Switches: Artillery.CapturedGunAmmo (used when PerGunAmmo is off), Artillery.PerGunAmmo,
// Artillery.GunDestruction, Artillery.GunDestructionChance.
// Write-up: notes/artillery.md

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace BugFixMod
{
    static class ArtyState
    {
        public static readonly AccessTools.FieldRef<Regiment, List<Regiment.GunSprite>> GunSprites =
            AccessTools.FieldRefAccess<Regiment, List<Regiment.GunSprite>>("gunsprite");
        public static readonly AccessTools.FieldRef<Regiment, int> SpritesPerGun =
            AccessTools.FieldRefAccess<Regiment, int>("artilleryspritespergun");

        // Ammo carried by a gun model while it sits in "LeftGuns" (PerGunAmmo).
        public static readonly ConditionalWeakTable<Artillery, float[]> GunAmmo = new ConditionalWeakTable<Artillery, float[]>();

        // GunDestruction per battery: D = real guns destroyed (excluded from capture),
        // Cap = intact real guns left (statsguns can never exceed it; -1 = no cap).
        public class Destroyed { public int D; public int Cap = -1; }
        public static readonly ConditionalWeakTable<Regiment, Destroyed> DestroyedGuns = new ConditionalWeakTable<Regiment, Destroyed>();

        public const string SideFile = "/bugfixmod_artillery.txt";

        public static void StoreAmmo(Artillery art, float[] ammo)
        {
            if (art == null || ammo == null || ammo.Length < 3) return;
            GunAmmo.Remove(art);
            GunAmmo.Add(art, new[] { ammo[0], ammo[1], ammo[2] });
        }

        public static int GetD(Regiment r) => r != null && DestroyedGuns.TryGetValue(r, out var d) ? d.D : 0;

        public static Destroyed Get(Regiment r) => r != null && DestroyedGuns.TryGetValue(r, out var d) ? d : null;

        public static float PerModel(Regiment r) => SpritesPerGun(r) * GamePrefs.gunspersprite;

        public static float Clamp01(float v) => float.IsNaN(v) ? 0f : Mathf.Clamp01(v);
    }

    // ------------------------------------------------------------------------------
    // 1. Captured guns (PerGunAmmo off): infantry/cavalry that grab abandoned guns get a
    //    detachment (BattleUnits.DetachCompany(unit, standardaridetachment, 2), called only
    //    from BattleUI.GrabLeftCannon). Vanilla clones the infantry regiment, so the new crew
    //    keeps ammo = {rifle fraction, 1, 1}. Set all three to CapturedGunAmmo instead.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.DetachCompany))]
    static class CapturedGunAmmo_Patch
    {
        static void Postfix(GameObject regiment, int targetunittype, int __result)
        {
            if (Cfg.PerGunAmmo.Value || __result != 0 || targetunittype != 2 || regiment == null) return;
            var src = regiment.GetComponent<Regiment>();
            if (src == null || (src.unittyp != 0 && src.unittyp != 1) || src.detachedregiment == null) return;
            var crew = src.detachedregiment.GetComponent<Regiment>();
            if (crew == null || crew.unittyp != 2) return;
            float c = ArtyState.Clamp01(Cfg.CapturedGunAmmo.Value);
            crew.ammo = new[] { c, c, c };
        }
    }

    // ------------------------------------------------------------------------------
    // 2. PerGunAmmo. Regiment.ammo stays "average fraction per gun" (consumption already
    //    divides by gun count). Guns leaving a battery carry that average with them; a unit
    //    grabbing guns takes the gun-weighted average of its own guns and the grabbed ones.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Regiment), nameof(Regiment.LeaveGuns))]
    static class PerGunAmmo_Leave_Patch
    {
        static void Prefix(Regiment __instance, int howmany)
        {
            if (!Cfg.PerGunAmmo.Value) return;
            var r = __instance;
            if (r.unittyp != 2 || r.guns <= 0 || howmany < 0 || r.ammo == null) return;
            var gs = ArtyState.GunSprites(r);
            if (gs == null) return;
            int start = howmany > 0 ? Mathf.Max(0, r.guns - howmany) : 0;
            for (int i = start; i < r.guns && i < gs.Count; i++)
                if (gs[i] != null) ArtyState.StoreAmmo(gs[i].artref, r.ammo);
        }
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.AdjustGunSprites))]
    static class PerGunAmmo_Adjust_Patch
    {
        static void Prefix(Regiment __instance)
        {
            if (!Cfg.PerGunAmmo.Value) return;
            var r = __instance;
            var gs = ArtyState.GunSprites(r);
            if (r.unittyp != 2 || gs == null || r.ammo == null) return;
            for (int i = Mathf.Max(0, r.guns); i < gs.Count; i++)
                if (gs[i] != null) ArtyState.StoreAmmo(gs[i].artref, r.ammo);
        }
    }

    [HarmonyPatch(typeof(Regiment), "GrabGuns")]
    static class PerGunAmmo_Grab_Patch
    {
        public struct State { public int guns; public float[] ammo; public List<GameObject> take; }

        static void Prefix(Regiment __instance, out State __state)
        {
            __state = new State
            {
                guns = __instance.guns,
                ammo = __instance.ammo != null ? (float[])__instance.ammo.Clone() : null,
                take = __instance.gunobjectstotake != null ? new List<GameObject>(__instance.gunobjectstotake) : null
            };
        }

        static void Postfix(Regiment __instance, State __state)
        {
            if (!Cfg.PerGunAmmo.Value || __state.take == null) return;
            var r = __instance;
            int n0 = Mathf.Max(0, __state.guns);
            int k = r.guns - n0;
            if (k <= 0 || __state.take.Count < k) return;
            float[] old = __state.ammo != null && __state.ammo.Length >= 3 ? __state.ammo : new[] { 1f, 1f, 1f };
            float fallback = ArtyState.Clamp01(Cfg.CapturedGunAmmo.Value);
            var sum = new float[3];
            for (int s = 0; s < 3; s++) sum[s] = old[s] * n0;
            for (int j = 0; j < k; j++)
            {
                var go = __state.take[j];
                var art = go != null ? go.GetComponent<Artillery>() : null;
                float[] a = null;
                if (art != null && ArtyState.GunAmmo.TryGetValue(art, out var stored)) a = stored;
                for (int s = 0; s < 3; s++) sum[s] += a != null ? a[s] : fallback;
                if (art != null) ArtyState.GunAmmo.Remove(art);
            }
            var res = new float[Mathf.Max(3, r.ammo != null ? r.ammo.Length : 3)];
            for (int s = 0; s < 3; s++) res[s] = ArtyState.Clamp01(sum[s] / (n0 + k));
            r.ammo = res;
        }
    }

    // ------------------------------------------------------------------------------
    // 3. GunDestruction. Each solid-shot hit from artillery on a battery's block may destroy
    //    one real gun. statsguns is crew-based in vanilla, so the battery gets a cap of intact
    //    guns (spare crew no longer helps) applied in an UpdateStatsGuns postfix, which also
    //    books the drop as gunslost. When the remaining
    //    real guns need fewer gun models, the model at the hit block is marked condition = 1
    //    and left on the field (same path as LeaveGuns), so it can never be recrewed.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(Projectile), "DestroyBullet")]
    static class GunDestruction_Hit_Patch
    {
        static readonly AccessTools.FieldRef<Projectile, int> AmmoType = AccessTools.FieldRefAccess<Projectile, int>("ammotype");
        static readonly AccessTools.FieldRef<Projectile, Regiment> Firer = AccessTools.FieldRefAccess<Projectile, Regiment>("firingunitreg");
        static readonly AccessTools.FieldRef<Projectile, List<Coy>> Hits = AccessTools.FieldRefAccess<Projectile, List<Coy>>("hittedobjects");

        static void Prefix(Projectile __instance)
        {
            if (!Cfg.GunDestruction.Value || AmmoType(__instance) != 0) return;
            var firer = Firer(__instance);
            var hits = Hits(__instance);
            if (firer == null || firer.unittyp != 2 || hits == null) return;
            float chance = Cfg.GunDestructionChance.Value;
            if (chance <= 0f) return;
            for (int i = 0; i < hits.Count; i++)
            {
                var coy = hits[i];
                if (coy == null) continue;
                var target = coy.assignedparentreg;
                if (target == null || target.unittyp != 2 || target.guns <= 0 || target.calledfromcampaign || target.alliance == firer.alliance) continue;
                if (UnityEngine.Random.Range(0f, 1f) >= chance) continue;
                int block = -1;
                for (int j = 0; j < target.blockobjectsused && target.blockobject != null && j < target.blockobject.Length; j++)
                    if (target.blockobject[j] == coy.gameObject) { block = j; break; }
                try { GunDestruction.DestroyOne(target, block); }
                catch (Exception e) { BugFixPlugin.Log.LogError("GunDestruction: " + e); }
            }
        }
    }

    static class GunDestruction
    {
        public static void DestroyOne(Regiment r, int block)
        {
            r.UpdateStatsGuns();
            if (r.statsguns <= 0) return;
            var st = ArtyState.DestroyedGuns.GetOrCreateValue(r);
            st.D++;
            st.Cap = r.statsguns - 1;
            r.UpdateStatsGuns(); // the cap books the destroyed gun as lost (postfix below)

            float perModel = ArtyState.PerModel(r);
            if (perModel <= 0f) return;
            int modelsNeeded = Mathf.CeilToInt(r.statsguns / perModel - 0.0001f);
            int excess = r.guns - Mathf.Max(0, modelsNeeded);
            if (excess <= 0) return;

            var gs = ArtyState.GunSprites(r);
            if (gs == null || gs.Count < r.guns) return;
            int hitGun = block >= 0 ? block / 3 : -1;

            if (modelsNeeded <= 0)
            {
                for (int i = 0; i < r.guns; i++) MarkDestroyed(gs[i]);
                r.LeaveGuns(); // battery has no guns left: same as being driven off them
                return;
            }
            for (int e = 0; e < excess; e++)
            {
                int last = r.guns - 1;
                int victim = (e == 0 && hitGun >= 0 && hitGun <= last) ? hitGun : last;
                if (victim != last) { var t = gs[victim]; gs[victim] = gs[last]; gs[last] = t; }
                MarkDestroyed(gs[last]);
                r.LeaveGuns(1);
            }
            r.SetRegimentSpritePos();
            r.updatesprites = 1;
        }

        static void MarkDestroyed(Regiment.GunSprite g)
        {
            if (g == null || g.artref == null) return;
            g.artref.condition = 1;
        }
    }

    [HarmonyPatch(typeof(Regiment), nameof(Regiment.UpdateStatsGuns))]
    static class GunDestruction_Stats_Patch
    {
        static void Postfix(Regiment __instance, bool adjuststatdata)
        {
            var st = ArtyState.Get(__instance);
            if (st == null || st.Cap < 0) return;
            int vanilla = __instance.statsguns;
            int target = Mathf.Min(vanilla, st.Cap);
            int delta = vanilla - target;
            if (delta == 0) return;
            __instance.statsguns = target;
            if (adjuststatdata)
            {
                __instance.gunslost += delta;
                __instance.gunscaptured -= delta;
            }
        }
    }

    // Grabbed guns add intact guns to a capped battery. Raised in the prefix so vanilla's own
    // UpdateStatsGuns call inside GrabGuns books the regained guns exactly as it would.
    [HarmonyPatch(typeof(Regiment), "GrabGuns")]
    static class GunDestruction_Grab_Patch
    {
        static void Prefix(Regiment __instance)
        {
            var st = ArtyState.Get(__instance);
            if (st == null || st.Cap < 0 || __instance.gunobjectstotake == null) return;
            int k = Mathf.Min(__instance.GetSollGuns(forinitialization: true), __instance.gunobjectstotake.Count);
            if (k <= 0) return;
            st.Cap = (__instance.guns <= 0 ? 0 : st.Cap) + Mathf.CeilToInt(k * ArtyState.PerModel(__instance));
        }
    }

    // Artillery.Start resets condition to 0, which would wipe a destroyed state loaded from a save.
    [HarmonyPatch(typeof(Artillery), "Start")]
    static class GunDestruction_KeepCondition_Patch
    {
        static void Prefix(Artillery __instance, out int __state) => __state = __instance.condition;
        static void Postfix(Artillery __instance, int __state)
        {
            if (Cfg.GunDestruction.Value && __state != 0) __instance.condition = __state;
        }
    }

    // AI picks the nearest left gun without checking condition; keep wrecks out of its list.
    [HarmonyPatch(typeof(BattleUnits), "UpdateLeftGuns")]
    static class GunDestruction_AIList_Patch
    {
        static void Postfix(BattleUnits __instance)
        {
            if (!Cfg.GunDestruction.Value || __instance.leftguns == null) return;
            __instance.leftguns.RemoveAll(g => g == null || (g.GetComponent<Artillery>() is Artillery a && a.condition != 0));
        }
    }

    // Destroyed guns are lost but not capturable: hide them from the winner's capture roll.
    [HarmonyPatch(typeof(BattleUnits), nameof(BattleUnits.SaveBattleResult))]
    static class GunDestruction_Capture_Patch
    {
        static void Prefix(BattleUnits __instance, out List<KeyValuePair<Regiment, int>> __state)
        {
            __state = null;
            if (!Cfg.GunDestruction.Value || __instance.allunitsofalliance == null) return;
            __state = new List<KeyValuePair<Regiment, int>>();
            foreach (var side in __instance.allunitsofalliance)
            {
                if (side?.unitsobjectrefreg == null) continue;
                foreach (var r in side.unitsobjectrefreg)
                {
                    int d = Mathf.Min(ArtyState.GetD(r), r != null ? r.gunslost : 0);
                    if (d <= 0) continue;
                    r.gunslost -= d;
                    __state.Add(new KeyValuePair<Regiment, int>(r, d));
                }
            }
        }

        static void Finalizer(List<KeyValuePair<Regiment, int>> __state)
        {
            if (__state == null) return;
            foreach (var kv in __state) if (kv.Key != null) kv.Key.gunslost += kv.Value;
        }
    }

    // ------------------------------------------------------------------------------
    // Battle save/load: per-gun ammo of left guns and destroyed-gun counts live only in
    // memory, so write a sidecar next to artillery.dat. Guns are matched by order (the same
    // GetComponentsInChildren order SaveArtillery/LoadArtillery use), batteries by name + commander.
    // ------------------------------------------------------------------------------
    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.SaveArtillery))]
    static class ArtySave_Patch
    {
        static void Postfix(string folder)
        {
            if (!Cfg.PerGunAmmo.Value && !Cfg.GunDestruction.Value) return;
            try
            {
                var ci = CultureInfo.InvariantCulture;
                var left = GameObject.Find("LeftGuns");
                var guns = left != null ? left.GetComponentsInChildren<Artillery>() : new Artillery[0];
                using (var w = new StreamWriter(folder + ArtyState.SideFile))
                {
                    w.WriteLine("guns " + guns.Length);
                    foreach (var a in guns)
                        w.WriteLine(ArtyState.GunAmmo.TryGetValue(a, out var am)
                            ? am[0].ToString(ci) + " " + am[1].ToString(ci) + " " + am[2].ToString(ci) : "-");
                    var regs = new List<Regiment>();
                    if (BattleUnits.completeunitlist != null)
                        foreach (var r in BattleUnits.completeunitlist) if (ArtyState.Get(r) != null) regs.Add(r);
                    w.WriteLine("regs " + regs.Count);
                    foreach (var r in regs)
                    {
                        var st = ArtyState.Get(r);
                        w.WriteLine(st.D + "\t" + st.Cap + "\t" + r.commander + "\t" + r.gameObject.name);
                    }
                }
            }
            catch (Exception e) { BugFixPlugin.Log.LogError("Artillery sidecar save: " + e); }
        }
    }

    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.LoadArtillery))]
    static class ArtyLoadGuns_Patch
    {
        static void Prefix(out HashSet<Artillery> __state)
        {
            __state = new HashSet<Artillery>();
            var left = GameObject.Find("LeftGuns");
            if (left != null) foreach (var a in left.GetComponentsInChildren<Artillery>()) __state.Add(a);
        }

        static void Postfix(string folder, HashSet<Artillery> __state)
        {
            string path = folder + ArtyState.SideFile;
            if (!File.Exists(path)) return;
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length == 0 || !lines[0].StartsWith("guns ")) return;
                int n = int.Parse(lines[0].Substring(5), CultureInfo.InvariantCulture);
                var left = GameObject.Find("LeftGuns");
                if (left == null) return;
                var fresh = new List<Artillery>();
                foreach (var a in left.GetComponentsInChildren<Artillery>()) if (!__state.Contains(a)) fresh.Add(a);
                if (fresh.Count != n) BugFixPlugin.Log.LogWarning("Artillery sidecar: " + n + " guns saved, " + fresh.Count + " loaded; matching by order");
                for (int i = 0; i < Mathf.Min(n, fresh.Count) && i + 1 < lines.Length; i++)
                {
                    var p = lines[i + 1].Split(' ');
                    if (p.Length < 3) continue;
                    ArtyState.StoreAmmo(fresh[i], new[] {
                        float.Parse(p[0], CultureInfo.InvariantCulture),
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture) });
                }
            }
            catch (Exception e) { BugFixPlugin.Log.LogError("Artillery sidecar load (guns): " + e); }
        }
    }

    [HarmonyPatch(typeof(ImportExportUnitData), nameof(ImportExportUnitData.LoadBattleUnitData))]
    static class ArtyLoadRegs_Patch
    {
        static void Postfix(string folder)
        {
            string path = folder + ArtyState.SideFile;
            if (!File.Exists(path) || BattleUnits.completeunitlist == null) return;
            try
            {
                var lines = File.ReadAllLines(path);
                int i = 0;
                while (i < lines.Length && !lines[i].StartsWith("regs ")) i++;
                for (i++; i < lines.Length; i++)
                {
                    var p = lines[i].Split(new[] { '\t' }, 4);
                    if (p.Length < 4) continue;
                    int d = int.Parse(p[0], CultureInfo.InvariantCulture);
                    int cap = int.Parse(p[1], CultureInfo.InvariantCulture);
                    int cmd = int.Parse(p[2], CultureInfo.InvariantCulture);
                    foreach (var r in BattleUnits.completeunitlist)
                        if (r != null && r.commander == cmd && r.gameObject.name == p[3] && r.unittyp == 2)
                        {
                            var st = ArtyState.DestroyedGuns.GetOrCreateValue(r);
                            st.D = d; st.Cap = cap;
                            break;
                        }
                }
            }
            catch (Exception e) { BugFixPlugin.Log.LogError("Artillery sidecar load (batteries): " + e); }
        }
    }
}
