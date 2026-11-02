// bugFixMod – unofficial bug fixes and small features for Grand Tactician: The Civil War
// BepInEx 5 plugin. Every fix and feature has its own switch in
// BepInEx/config/aom.bugfixmod.cfg (created on first launch).

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BugFixMod
{
    [BepInPlugin(Guid, "bugFixMod", Version)]
    public class BugFixPlugin : BaseUnityPlugin
    {
        public const string Guid = "aom.bugfixmod";
        public const string Version = "1.0.12";

        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Cfg.Bind(Config);
            // Patch each class on its own: Harmony.PatchAll stops at the first failure, which
            // silently left every later patch unapplied.
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(BugFixPlugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Log.LogError("Patch " + type.Name + " failed, that fix is inactive: " + (e.InnerException ?? e).Message);
                }
            }
            gameObject.AddComponent<PerfLogger>();
            Log.LogInfo("bugFixMod " + Version + " loaded: " + ok + " patches applied, " + failed + " failed");
        }
    }

    internal static class Cfg
    {
        // --- Campaign ---
        public static ConfigEntry<bool> EmptyHQRules, AiLoneHQ, FleetBombardBesiegedFort, LevelThreeForts, AutoresolveFixes;
        public static ConfigEntry<bool> FleetDisintegrationLoopFix, FleetDisintegrationScuttleShips;
        public static ConfigEntry<float> FleetDisintegrationRepeatDays;
        public static ConfigEntry<bool> AutoresolveWeaponQuality, AutoresolveAccuracyMatchesBattle;
        public static ConfigEntry<float> AutoresolveRofExponent, AutoresolveRangeExponent, AutoresolveAccuracyExponent;
        public static ConfigEntry<bool> AiWeaponValue, AiWeaponPurchases, CareerCicPath;
        public static ConfigEntry<bool> TariffPerConnection, ImportPriceRelationsPerSide, DesertersGoHome, DesertersNotPrisoners, DeserterStatistic, PerfLog;
        public static ConfigEntry<float> PerfLogSeconds;
        public static ConfigEntry<float> DeserterReturnShare, DeserterReturnRate;
        public static ConfigEntry<bool> PowCampPriority, ParoleExcessOnly, RaidFreesPows, RaidTimerFix, PardonAddsRecruits;
        public static ConfigEntry<float> RaidPowFreeShare;
        // --- Battle bug fixes ---
        public static ConfigEntry<bool> EndOfDayFreeze, SaveReferences, ArtilleryLineOfFire;
        public static ConfigEntry<float> ArtilleryNearDistance;
        public static ConfigEntry<bool> KeepCombatBehaviourOnAttackMove, CavalryButtonOff, SkirmisherFallback, RoutedReattachAvoidsEnemy;
        public static ConfigEntry<bool> ColumnBackStep, ChargeEndRestoresAllWaypoints, HqSpeedFix, CustomBattleReattach;
        public static ConfigEntry<bool> OrderInterruptionVisibleOnly, OrderInterruptionStaleClear;
        public static ConfigEntry<bool> CumulativeMeleeSurrender, RunDownSurrender, InfantryPursuit;
        public static ConfigEntry<float> SurrenderMeleeHours, SurrenderMeleeDecay, RunDownChancePerHour, RunDownRange, RunDownSpeedMargin, InfantryPursuitMaxFatigue;
        // --- Artillery features ---
        public static ConfigEntry<bool> PerGunAmmo, GunDestruction;
        public static ConfigEntry<float> GunDestructionChance, CapturedGunAmmo;
        // --- Reporting / DLC ---
        public static ConfigEntry<bool> OobPerformance, SaveMenuPerformance;
        public static ConfigEntry<bool> ExtendedBattleReport, DlcCapturePrestige;
        public static ConfigEntry<float> PrestigePerOfficerRank, PrestigePerCapturedWeaponValue;

        public static void Bind(ConfigFile c)
        {
            EmptyHQRules = c.Bind("Campaign", "EmptyHQRules", true,
                "An HQ with no troops on the spot only starts or joins a battle or siege when a friendly force with troops is in range; corps/division/brigade HQs with no units left at all withdraw. Sieges where one side has no fighting men left end immediately.");
            AiLoneHQ = c.Bind("Campaign", "AiLoneHQ", true,
                "AI HQs with no troops on the spot are not sent into enemy territory; they rejoin the nearest friendly force instead.");
            FleetBombardBesiegedFort = c.Bind("Campaign", "FleetBombardBesiegedFort", true,
                "Fleets can start bombarding a fort whose garrison is already under siege.");
            LevelThreeForts = c.Bind("Campaign", "LevelThreeForts", true,
                "Fix the crash that freezes sieges and bombardments of level-3 forts. Uses weapon group 3 for level-3 forts if weapongroups.dat has one, else falls back to the level-2 group.");
            FleetDisintegrationLoopFix = c.Bind("Campaign", "FleetDisintegrationLoopFix", true,
                "A beaten fleet with no retreat path is no longer teleported to a harbor with enemies nearby (it picks the closest safe friendly harbor). If none exists, or it already escaped this way within RepeatDays, the fleet disintegrates for good (vanilla removal: ships return to the pool, commander unassigned) instead of ping-ponging between harbors.");
            FleetDisintegrationRepeatDays = c.Bind("Campaign", "FleetDisintegrationRepeatDays", 7f,
                "Campaign days after a fleet's escape to a harbor during which a second escape removes the fleet instead.");
            FleetDisintegrationScuttleShips = c.Bind("Campaign", "FleetDisintegrationScuttleShips", false,
                "When FleetDisintegrationLoopFix removes a fleet, sink its ships instead of returning them to the ship pool.");

            AutoresolveFixes = c.Bind("Campaign", "AutoresolveFixes", true,
                "Auto-resolve: fix swapped Sharpshooter/Zouave perks, entrenchment always using side 1, counter-battery chance indexed by side instead of stance, and random picks that never choose the last unit. Requires restart.");
            AutoresolveWeaponQuality = c.Bind("Autoresolve", "WeaponQuality", true,
                "Auto-resolve small-arms fire uses rate of fire, range and accuracy as well as firepower (vanilla: firepower only, so smoothbore muskets beat everything). Also fixes weapon id 0 (6-pounder) batteries inflicting no casualties. Requires restart.");
            AutoresolveRofExponent = c.Bind("Autoresolve", "RofExponent", 0.7f,
                "Weight of rate of fire: factor (ROF / 3)^x. 1 = proportional (repeaters very strong), 0 = ignore. Also used by the AI weapon valuation (Campaign.AiWeaponValue).");
            AutoresolveRangeExponent = c.Bind("Autoresolve", "RangeExponent", 0.5f,
                "Weight of range: factor (range yd / 350)^x at defend distance, half at attack distance, none at assault distance.");
            AutoresolveAccuracyExponent = c.Bind("Autoresolve", "AccuracyExponent", 2f,
                "Weight of accuracy: factor accuracy^x (scatter is two-dimensional, so 2 matches the battle maths).");
            AutoresolveAccuracyMatchesBattle = c.Bind("Autoresolve", "AccuracyMatchesBattle", true,
                "true: accuracy is blended with experience and morale and clamped at 1 exactly as in battle (so it rarely matters). false: raw weapon accuracy relative to 0.9.");
            AiWeaponValue = c.Bind("Campaign", "AiWeaponValue", true,
                "AI weapon rating (used for AI purchases, AI re-arming, side averages and DLC upgrade prestige costs) uses the auto-resolve weapon formula instead of vanilla's accuracy-cubed one, scaled so the Springfield and Napoleon keep their vanilla ratings. Career prestige costs and captured-weapon prestige keep the vanilla rating. Requires restart.");
            AiWeaponPurchases = c.Bind("Campaign", "AiWeaponPurchases", true,
                "AI weapon orders: the biggest rating gain per dollar among weapons better than the side's average, sized to what its units actually need (minus stock and pending orders), capped by vanilla's order size. Vanilla: a random better weapon, always 27,000 rifles / 7 batteries, price ignored.");
            TariffPerConnection = c.Bind("Campaign", "TariffPerConnection", true,
                "The tariff markup on goods applies only to links with another side's hubs. Vanilla never resets it, so domestic links listed after a hub's first foreign link are treated as imports. Requires restart.");
            ImportPriceRelationsPerSide = c.Bind("Campaign", "ImportPriceRelationsPerSide", true,
                "European weapon import prices use each side's own relations. Vanilla gives both sides the Confederate figure, so Europe favouring the Confederacy also cheapens Union imports. Requires restart.");
            DesertersNotPrisoners = c.Bind("Campaign", "DesertersNotPrisoners", true,
                "Deserters are no longer handed to the enemy as prisoners after a unit's next battle (vanilla counts the unit's whole desertion tally as captured, and does so again after every later battle).");
            PerfLog = c.Bind("Performance", "PerfLog", false,
                "Log the game's own per-section frame timers (average ms per frame) to LogOutput.log. For profiling slow battles; leave off otherwise.");
            PerfLogSeconds = c.Bind("Performance", "PerfLogSeconds", 15f,
                "Seconds between PerfLog reports.");
            DeserterStatistic = c.Bind("Campaign", "DeserterStatistic", true,
                "The per-state Deserters figure counts men when they desert. Vanilla adds every unit's whole running desertion tally again after each battle, so the figure balloons.");
            DesertersGoHome = c.Bind("Campaign", "DesertersGoHome", true,
                "Men leaving at contract expiry and deserters return to their home state's workforce and recruit pool instead of vanishing. A share of deserters is returned to the army over time (see DeserterReturnShare/Rate).");
            DeserterReturnShare = c.Bind("Campaign", "DeserterReturnShare", 0.35f,
                "Share of deserters later returned to the army (arrests, amnesties). Historical guide: the Union arrested 76,526 of about 200,000 recorded deserters.");
            DeserterReturnRate = c.Bind("Campaign", "DeserterReturnRate", 3f,
                "Rate per year at which absent deserters are returned (3 = on average four months away). They fill under-strength infantry/cavalry from their state, or go home if none has room.");
            PardonAddsRecruits = c.Bind("Campaign", "PardonAddsRecruits", true,
                "Paroled prisoners and prisoners freed from camps go back into their side's recruiting pool. Vanilla has the sign reversed: every released prisoner removes a recruit (and workforce) from his own side.");
            PowCampPriority = c.Bind("Campaign", "PowCampPriority", true,
                "New prisoners (and overflow transfers) go to the nearest camp with room in a state loyal to its owner; camps in captured (not loyal) states are used only when no loyal camp has room. Vanilla: nearest camp, room ignored.");
            ParoleExcessOnly = c.Bind("Campaign", "ParoleExcessOnly", true,
                "The Parole button on an over-capacity POW camp releases only the prisoners above capacity. A camp within capacity paroles everyone, as in vanilla.");
            RaidFreesPows = c.Bind("Campaign", "RaidFreesPows", true,
                "Raiding an enemy POW camp frees prisoners in proportion to the damage done: RaidPowFreeShare of them by the time its condition reaches 0. Freed men return to their side's recruiting pools like paroled men.");
            RaidPowFreeShare = c.Bind("Campaign", "RaidPowFreeShare", 0.5f,
                "Share of a camp's prisoners freed by raiding it from full condition down to 0.");
            RaidTimerFix = c.Bind("Campaign", "RaidTimerFix", true,
                "Raid loot, stock losses and support changes use the time since the last raid update, not the time since the raid first began (vanilla never advances the timer, so effects snowball the longer a raid runs).");
            CareerCicPath = c.Bind("Campaign", "CareerCicPath", true,
                "Career: while you command a top-level army at the commander-in-chief's rank or higher, the quarterly commander-in-chief review scores you as an army-group commander like the incumbent, so rank, experience and fame decide. Vanilla always scores the incumbent higher.");
            EndOfDayFreeze = c.Bind("Battle", "EndOfDayFreeze", true,
                "Fix multi-day battles where every unit is frozen after the night/deployment phase.");
            SaveReferences = c.Bind("Battle", "SaveReferences", true,
                "More robust battle saves: unit cross-references (detachments, parent brigades, targets) are matched by name and commander instead of only by position.");
            ArtilleryLineOfFire = c.Bind("Battle", "ArtilleryLineOfFire", true,
                "Artillery won't fire at a target if the shot's arc passes through a nearby friendly block, or would hit the ground short of the target next to friendly troops. Friendly fire itself stays on.");
            ArtilleryNearDistance = c.Bind("Battle", "ArtilleryNearDistance", 40f,
                "How far from the gun (map units, about 6.3 m each) the arc is checked for passing through friendly blocks.");
            KeepCombatBehaviourOnAttackMove = c.Bind("Battle", "KeepCombatBehaviourOnAttackMove", true,
                "Attack moves and finished charges no longer reset a unit's combat behaviour (cavalry charge, infantry engage).");
            CavalryButtonOff = c.Bind("Battle", "CavalryButtonOff", true,
                "Cavalry Charge-off and Evade-off buttons actually switch the behaviour off (vanilla sends a value the unit rejects).");
            SkirmisherFallback = c.Bind("Battle", "SkirmisherFallback", true,
                "Skirmishers and evading cavalry fall back directly away from a close enemy when the enemy is between them and their parent, instead of jittering.");
            RoutedReattachAvoidsEnemy = c.Bind("Battle", "RoutedReattachAvoidsEnemy", true,
                "Routed detachments path around enemy units on their way back to their parent.");
            HqSpeedFix = c.Bind("Battle", "HqSpeedFix", true,
                "HQs moved on their own are no longer held to the speed of the slowest unit in their group (only units moving under the same order count). On the campaign map, HQs with no units no longer move at 70 % speed.");
            CustomBattleReattach = c.Bind("Battle", "CustomBattleReattach", true,
                "Historical/custom battles: player regiments that the battle file places individually are no longer silently detached from their brigade (they ignored every group order).");
            ColumnBackStep = c.Bind("Battle", "ColumnBackStep", true,
                "Units in column no longer step back one row before following a new path.");
            ChargeEndRestoresAllWaypoints = c.Bind("Battle", "ChargeEndRestoresAllWaypoints", true,
                "After a charge ends, all of the unit's previous waypoints are restored, not only the last one.");
            OrderInterruptionVisibleOnly = c.Bind("Battle", "OrderInterruptionVisibleOnly", true,
                "Player units only stop a plain move/formation order for enemy contact when the enemy is visible (not hidden by fog of war). Routed or destroyed enemies never count, for either side.");
            OrderInterruptionStaleClear = c.Bind("Battle", "OrderInterruptionStaleClear", true,
                "A new move or formation order clears a stuck 'path interrupted' state on the player units it reaches unless a visible enemy is within their stopping distance, and stuck units clear it themselves once no visible enemy is near (vanilla never clears it in career battles).");

            CumulativeMeleeSurrender = c.Bind("Surrender", "CumulativeMeleeSurrender", true,
                "Routed units surrender after enough total time in melee, counted cumulatively (vanilla needs one unbroken melee armed at the moment of routing, which almost never happens).");
            SurrenderMeleeHours = c.Bind("Surrender", "SurrenderMeleeHours", 0.1f,
                "Total game hours of melee while routed before the unit surrenders (0.1 = 6 minutes; vanilla countdown is 0.22).");
            SurrenderMeleeDecay = c.Bind("Surrender", "SurrenderMeleeDecay", 0.25f,
                "How fast that total drains while out of contact, relative to how fast it builds (0.25 = a quarter as fast).");
            RunDownSurrender = c.Bind("Surrender", "RunDownSurrender", true,
                "Routed units can surrender when faster enemies are on top of them, without melee. Scaled by pursuer vs routed strength, so small cavalry units rarely run down large brigades.");
            RunDownChancePerHour = c.Bind("Surrender", "RunDownChancePerHour", 3f,
                "Base surrender rate per game hour while being run down, before size, fatigue, losses and experience modifiers.");
            RunDownRange = c.Bind("Surrender", "RunDownRange", 10f,
                "Distance (map units, ~6.3 m each) within which a faster enemy counts as running the unit down.");
            RunDownSpeedMargin = c.Bind("Surrender", "RunDownSpeedMargin", 1.1f,
                "A pursuer must be this many times faster than the routed unit to count.");
            InfantryPursuit = c.Bind("Surrender", "InfantryPursuit", true,
                "Infantry keeps chasing a charge target that routs (vanilla: only cavalry does), within the charge radius.");
            InfantryPursuitMaxFatigue = c.Bind("Surrender", "InfantryPursuitMaxFatigue", 0.6f,
                "Infantry stops pursuing above this fatigue.");
            PerGunAmmo = c.Bind("Artillery", "PerGunAmmo", false,
                "Artillery ammunition belongs to the guns: captured guns keep the ammunition left in them, and a battery that loses guns loses their share.");
            CapturedGunAmmo = c.Bind("Artillery", "CapturedGunAmmo", 0.1f,
                "When PerGunAmmo is off: shell/canister/solid-shot fraction given to infantry or cavalry that capture guns (vanilla effectively gives them full ammunition).");
            GunDestruction = c.Bind("Artillery", "GunDestruction", false,
                "Guns can be destroyed by solid shot. Destroyed guns cannot be recrewed or captured.");
            GunDestructionChance = c.Bind("Artillery", "GunDestructionChance", 0.05f,
                "Chance that a solid-shot hit on an artillery company block destroys one of its guns.");

            OobPerformance = c.Bind("Performance", "CampaignOOB", true,
                "Build the campaign OOB once per click instead of 6-9 times, skip hidden split-view windows, and avoid repeated strength recalculation while building.");
            SaveMenuPerformance = c.Bind("Performance", "SaveMenu", true,
                "Save/load menu builds its list once instead of twice, and caches flag sprites.");
            ExtendedBattleReport = c.Bind("Reporting", "ExtendedBattleReport", true,
                "Battle results list captured weapons by type, and officers killed, wounded or captured by name and rank.");
            DlcCapturePrestige = c.Bind("Reporting", "DlcCapturePrestige", true,
                "Career DLC: gain prestige for captured high-quality weapons and enemy officers.");
            PrestigePerOfficerRank = c.Bind("Reporting", "PrestigePerOfficerRank", 10f,
                "Career DLC: prestige per rank step of each enemy officer captured, multiplied by (0.1 + that officer's fame); half for killed/wounded.");
            PrestigePerCapturedWeaponValue = c.Bind("Reporting", "PrestigePerCapturedWeaponValue", 0.1f,
                "Career DLC: multiplier on prestige for captured weapons that are better than the side's average weapon (0.1 = 10 % of the base formula).");
        }
    }
}
