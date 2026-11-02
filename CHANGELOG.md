# Changelog

Full explanations of every bug and fix are in the README. Each change has its own switch in `BepInEx/config/aom.bugfixmod.cfg`.

## 1.0.12 – first public release
- Optional battle profiler (`Performance.PerfLog`): logs the game's own per-section frame timers, which the game records but never displays.

## 1.0.11
- The per-state Deserters figure counts men when they desert, instead of re-adding every unit's whole desertion tally after every battle.
- Returning deserters respect the game's real unit-size cap.

## 1.0.10
- Deserters are no longer handed to the enemy as prisoners (vanilla did this after every battle a unit fought).
- Men whose contracts expire, and deserters, go back to their home state's workforce and recruit pool. A share of deserters (default 35 %, based on Union arrest figures) return to the army over a few months.

## 1.0.9
- The tariff markup applies only to trade with other sides' hubs (vanilla also dampened some domestic trade).
- Union weapon import prices follow Europe's relations with the Union, not with the Confederacy.

## 1.0.8
- Paroled and liberated prisoners add recruits to their side; vanilla removed them.

## 1.0.7
- New prisoners go to POW camps in loyal territory with room before captured camps.
- The Parole button on an overcrowded camp releases only the excess.
- Cavalry raids on enemy POW camps free prisoners.
- Raid effects no longer snowball with raid length (the raid timer never advanced).
- Career prestige costs and captured-weapon prestige use the vanilla weapon rating again.
- AI weapon orders are no longer capped by the treasury.

## 1.0.6
- AI weapon rating uses the auto-resolve weapon formula instead of accuracy cubed.
- AI weapon orders are sized to need and chosen by value per price (no more 27,000-rifle Whitworth orders).
- Career: the player commanding a top-level army can become commander-in-chief.
- Auto-resolve rate-of-fire weight raised to 0.7.

## 1.0.0 – 1.0.5 (development)
- Campaign: empty-HQ battle and siege rules, phantom sieges, AI lone HQs, fleet bombardment of besieged forts, level-3 fort freeze, fleet disintegration ping-pong, auto-resolve fixes (perks, entrenchment, counter-battery, random picks, weapon quality, 6-pounders).
- Battle: multi-day freeze, battle save references, artillery line of fire, attack-move behaviour, cavalry buttons, skirmisher jitter, routed detachments, column back-step, charge waypoints, HQ speed, custom-battle reattachment, order interruption by hidden enemies.
- Surrender and pursuit: cumulative melee surrender, run-down surrender, infantry pursuit.
- Artillery options: captured-gun ammo, per-gun ammo, gun destruction.
- Extended battle report and career capture prestige.
- Campaign OOB and save menu performance.
- Per-class patching so one failed patch can't disable the rest.
