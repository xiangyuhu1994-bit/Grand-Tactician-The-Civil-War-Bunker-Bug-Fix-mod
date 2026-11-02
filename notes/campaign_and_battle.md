## Campaign fixes (src/Campaign.cs)

### Empty HQs starting battles and sieges – `EmptyHQRules`
**Bug.** An HQ with no units under it can start a siege, as attacker or defender, and the opposing force loses readiness and time to a siege against nobody.
- `Regiment.UpdateGroupStrength` hardcodes `groupmorale = 1` for an HQ with no attached units (Regiment.cs ~11319). It also forces `groupstrengthactive` to 1 (~11646).
- So the empty HQ passes the minimum-morale check in `BattleUnits.EstablishCampaignBattle` (BattleUnits.cs ~2933).
- Siege-type battles – a garrison is involved, or both sides are entrenched (`campaignstance == 1`) – skip the strength-ratio checks entirely (~3079).
- The siege then runs its setup time (`setuptimesiege[level]`, days) while draining readiness (`readinessconsumptionsieges`). Only after setup does the force-balance check notice one side has nothing and end it (Autocalc.cs ~2230–2265).

**Fix.** A prefix on `EstablishCampaignBattle`:
- **Empty army HQ (unittyp 18):** may start or join a battle or siege only if a friendly unit with troops is in range. "In range" uses the tests vanilla's `CheckBattleParticipation` uses to pull units into a fight: bugle range for sieges, command range otherwise, morale above the participation minimum, and the terrain-line check. When supported, vanilla runs unchanged, so the army commander can oversee the fight.
- **Unsupported empty army HQ:**
  - If it is the initiator, nothing happens – no battle and no withdrawal.
  - If an enemy with troops runs into it, it withdraws (`SetRetreatToDepot`, as vanilla does for low-morale units), so it can't sit there unattackable.
- **Empty corps, division or brigade HQs:** always withdraw.
- "Empty" means `groupstrength <= 0`. The raw sum is used because `groupstrengthactive` is faked to 1.


**Update (1.0.2) – why phantom sieges still happened, and three more fixes.**
- **"Empty" now means no troops on the spot.** An HQ's `groupstrength` counts every subordinate wherever it is. An HQ that had marched ahead of its divisions therefore looked full to the first version of this fix, and could still open a siege on its own. "Empty" now counts only fighting units under the HQ that are within its bugle range (`LocalStrength`). Friendly support is judged the same way.
  - Corps, division and brigade HQs with no units left anywhere still withdraw.
  - An HQ whose units are merely elsewhere is treated like an army HQ: it may fight only with friendly troops in range.
- **Sieges with nobody left on one side end at once** (`PhantomSiege_Patch`, prefix on `Autocalc.UpdateSiegeCombat`).
  - **Cause:** vanilla returns early while siege works are being set up (`setuptimesiege[level]`, about a day) – before it checks whether either side can still fight. A siege whose only attackers have left keeps draining the defenders' readiness until setup ends.
  - **Fix:** each update, if one side has no fighting men (non-withdrawn, not retreating units of unittyp ≤ 13 with strength), the siege ends through the game's own `FinishLandBattle`, with the other side winning. This also clears phantom sieges already running in a save.
- **AI no longer marches lone HQs into enemy territory** (`AiLoneHQ_Patch`, `Campaign.AiLoneHQ`).
  - `AICampaign.MoveUnitTo` (~5770) is the AI's single move order, and it is used for HQs with no troops too.
  - If the AI sends an HQ with no troops on the spot to a point on the enemy side of the front (`frontline2.GetSideOnPosition`), it is sent to the nearest friendly force with troops instead. If there is no such force, it stays put.

### Fleets can't bombard a besieged fort – `FleetBombardBesiegedFort`
**Bug.** This is an ordering problem in `EstablishCampaignBattle`.
- A besieged garrison has `inbattle = true`.
- The "either side already in a battle?" branch (BattleUnits.cs ~2954) comes before the fleet-bombardment branch (~3001).
- That branch only lets fleets join fleet battles and land units join land battles, then returns.
- So a fleet meeting a besieged garrison never reaches the bombardment code.
- The reverse works: `Autocalc.StartFortBombardment` doesn't set `inbattle` on the garrison, so a land army can still start a siege on a fort under bombardment.

**Fix.** The prefix runs the vanilla bombardment start (same conditions: fort condition and bugle range) for a fleet versus a garrison that is already in battle.
- Nothing in the bombardment update (`UpdateFortNavyEngagement`) reads `inbattle`.
- Siege and bombardment together is already a state vanilla reaches (bombard first, then besiege).
- Small extra fix: vanilla leaves a dead `Autocalc` component attached when `StartFortBombardment` returns null, which blocks later bombardments. The patch destroys it.

### Level-3 forts – `LevelThreeForts`
**Bug.**
- `GameVars.weapongroupforts` (fort level → weapon group) is hardcoded to `{ 0, 1, 2 }` (GameVars.cs:6028).
- Most callers clamp the level, but `Autocalc.GetAveragePenetrationFort` (Autocalc.cs:582, fleet bombardment), `GetTotalStrength(useforvictorycondition)` (:628, sieges) and `AICampaign` (:6135) don't.
- For a level-3 fort these throw `IndexOutOfRangeException` inside `Autocalc.Update`, so the siege or bombardment aborts every frame and never progresses or ends.

**Fix.** A postfix on `GameVars.ImportWeapons` sets the map to `{ 0, 1, 2, 3 }`.
- `weapongroups.dat` already has group 3 ("Fort Armory Level 3"), so level-3 forts now get their own guns instead of crashing.
- Level-indexed prefs arrays such as `setuptimesiege` have 6 entries, so level 3 is safe there.

## Battle fixes (src/Battle.cs)

### Artillery firing into its own lines on hills – `ArtilleryLineOfFire`
**Bug.**
- Targeting has no terrain line of sight. The only own-unit test, `Regiment.CheckOwnUnitInWay` (Regiment.cs ~9613), is a flat angle check. It also lets artillery overshoot any friendly unit at least 10 units away (`mindistanceownunitovershooting`, unitprefs.txt).
- So a gun happily fires over a ridge or over a friendly line sitting on a slope. The shell meets the ground short of the target, near that line.
- Direct-path hits skip own-side blocks (Projectile.cs ~345). But the fragment rays from `Projectile.GetExplosivesHits` (~298–318) have no alliance check, so the burst hits your own troops.

**Fix.** Friendly fire stays on; the gun just won't take the shot. When an artillery gun picks a target, `FireBullet` asks `CheckOwnUnitInWay` once per candidate enemy unit. A postfix there traces the shot's arc the same way `Projectile` does: a sine arc whose peak is `weapon.trajectorypeak / firerange × distance / battlefieldrealdimensionfactor`. The target counts as blocked if either:
- the arc passes through a friendly company block within `ArtilleryNearDistance` of the gun (default 40 units, about 250 m), or
- the arc meets the ground short of the target with a friendly block within fragment range of that point (`projectileweight × projectileweightratiorange`, at least 2 units). A landing within that distance of the target counts as a normal shot.

A blocked target is skipped exactly like vanilla's own-unit-in-way case, so the gun picks another target or holds fire.

Limitations:
- The trace uses the aim point without scatter, so a wide miss can still land near friends.
- Solid shot's bounce after landing isn't traced.

### Attack moves wipe combat behaviour – `KeepCombatBehaviourOnAttackMove`
**Bug.** `SetMovementMode(3)` (attack move) sets `combatbehavior = 0` for infantry and cavalry (Regiment.cs ~15828). Mode 3 is entered by:
- the Assault order
- mounted Advance (converted to mode 3)
- an infantry right-click while Charge is on
- the end of every tracked charge

The effects:
- Cavalry drop "charge" after their first charge.
- Infantry drop "engage" (3) the moment they move.
- The fire-one-volley-then-charge logic in `UpdateChargeTarget` needs mode 3 and behaviour 3 together, so it is unreachable.

**Fix.** A prefix/postfix pair keeps behaviours 3 and 6 across that call. Range behaviours 0–2 are still reset to close range when attacking, as vanilla intends.

### Cavalry Charge-off / Evade-off buttons do nothing – `CavalryButtonOff`
**Bug.**
- Both buttons send behaviour 5 (PanelHandler.cs ~3146/3198).
- `SetCombatBehavior` rejects 5 for every non-artillery unit (Regiment.cs ~13212).
- The button changes state but the unit stays in 3, 4 or 6.

**Fix.** For cavalry, 5 is mapped to 0 ("fire normally"). Nothing in the unit logic treated 5 specially anyway.

Not changed: cavalry start a battle in behaviour 6, where right-click always charges and there is no fire order. Toggling the button off and on gives 3, where right-click fires and Alt charges. Behaviour 6 is never read by the unit logic – it is purely a UI flag – so mapping the starting 6 to 3 would be a one-line patch if you prefer that control scheme.

### Skirmisher jitter when the enemy gets between them and their brigade – `SkirmisherFallback`
**Bug.** `CheckFallbackDueToEnemyClose` (Regiment.cs ~19982) runs every cycle for skirmishers, scouts, screening units and evading cavalry.
- It triggers when the nearest enemy is within `minimumdistancetonextenemyunittofallback` (26).
- The fallback point is 31 m from the enemy, on the line from the enemy towards the parent brigade (~20037). The unit's own position isn't used.
- If the unit is between enemy and parent, the point is behind it and everything works.
- If the enemy is between them, or off to the side, the point is on the far side of the enemy. The unit never gets clear.
- Every time the target drifts 5 m (`triggertorenewfallback`) it gets a new path and a new facing – the spinning.

**Fix.** A prefix detects the bad geometry: either the vanilla target doesn't lie away from the enemy as seen from the unit, or the enemy is closer to the parent than the unit is. In that case it uses the same construction on the line from the enemy through the unit itself – straight away from the enemy. Normal geometry and the in-melee case are left to vanilla.

### Routed detachments run through enemies to their brigade – `RoutedReattachAvoidsEnemy`
**Bug.**
- `Rout()` plots an enemy-avoiding path: a `Retreat.RetreatOrder` turns on enemy NavMesh obstacles, waits two frames for them to carve, then paths.
- But every cycle `CheckRoutUpdate`/`CheckRoutUpdate2` give a routed unit with a parent a reattach order.
- `CheckForReattachmentOrder` then paths straight at the parent with a plain NavMesh path (Regiment.cs ~20629), overriding the rout path.
- Routed units still trigger melee on contact, so they take casualties on the way through.

**Fix.** While a routed unit is inside `CheckForReattachmentOrder`, its `RegimentSetPath` call is replaced by a `Retreat.RetreatOrder` to the same destination – the mechanism couriers use to path around enemies. Running back to the parent is kept, since that part looks intended.

### Column steps back before following a new path – `ColumnBackStep`
**Bug.**
- The regiment transform is snapped to block 0 every cycle (`AdjustRegimentParentPositions`, Regiment.cs ~25782).
- In column, block 0 sits one row behind the head; the colour block is at the front (`ChangeRegimentFormation`, ~26516).
- A new path starts at the regiment position and lays out each block's slot for the first corner behind it. So every block's first target is one row (`blocksize` × 10 = 2.5 units, about 16 m) behind where it stands.
- `RegimentMove` only advances a block once it reaches its current corner. So the whole column marches back a row, turns, then advances.
- In line, block 0 is front-centre, so there is no offset – that's why it shows mainly in column.

**Fix.** A postfix on `RegimentSetPath`: for a fresh single path, blocks still aiming at corner 0 skip to corner 1. It is skipped when another prefix replaced the call.

### Charge end loses waypoints – `ChargeEndRestoresAllWaypoints`
**Bug.** When a tracked charge reaches its target, `UpdateChargeTarget` restores the waypoints the unit had before charging. It does this by calling `RegimentSetPath(newpath: true)` once per waypoint (Regiment.cs ~13383). Each call wipes the path before it, so only the last waypoint survives.

**Fix.** A prefix handles exactly that case first, with the same conditions: mode 6, target reached, target not routed. It appends the waypoints, using `newpath` only for the first. It then clears the charge target, so vanilla's block sees nothing to do.

### Found but not patched
- **Charge odds check (`UpdateChargeTarget`):** the strength weight is applied to both sides, so it cancels out.
- **Charge radius:** measured from the parent HQ. For independent units the parent is the root container, so their auto-charge never fires.
- **Counter-charge on contact:** skips block 0 (`<= 0` where `< 0` is meant, Regiment.cs ~17049).
- **Melee formation term:** `1 + defender_mod − attacker_mod` has no clamp, so e.g. a column attacked by skirmishers gets −0.75 – negative casualties.
- **Melee cavalry bonus:** still applies when the cavalry are dismounted.
- **Speed shock:** reads the defender's `isrouted` and position change instead of the attacker's.

### HQs crawl when moved on their own – `HqSpeedFix`
**Bug (battle).**
- Every unit caps its speed at its order-giver's "group speed": `speed *= min(groupspeed, 1)` (Regiment.cs ~24983).
- `UpdateGroupSpeed` (~31046) sets that to the slowest unit in the order-giver's whole group whenever the order-giver is an HQ. That includes an HQ moved on its own, when none of its units are moving.
- So an HQ (base speed 2) moved by itself drops to about half speed whenever its group contains unlimbered foot artillery (0.535). That's why it only happens "sometimes".

**Fix.** A postfix on `UpdateGroupSpeed` recomputes group speed from units actually moving under the same order. If none are, there is no cap.

**Bug (campaign).**
- Campaign speed is scaled by group training: `movementspeedtraining` runs from 0.7 at training 0 to 1.2 at training 1.
- An HQ with no units has nothing to average, so its training stays 0 and it always moves at 70 %.

**Fix.** An HQ with no units gets group training 0.6, a neutral 1.0 speed factor.

**Not the cause:** HQ cohesion or fatigue. HQs recover fatigue like any unit (`UpdateFatigue`).

### Historical/custom battles: some regiments ignore every group order – `CustomBattleReattach`
**Bug.**
- Battle files can place single regiments individually. These go into `BattleUnits.initialregimentoverride`.
- `AllocateUnitsAndObjectivesToAI` (BattleUnits.cs ~7566) then sets `permanentlydetached` on every such player regiment, with no condition.
- Group orders (`SetGroupFormation`, group moves) silently skip detached units (`IsAttached`, BattleUnits.cs:9108; `GetAttachedUnitsReg` excludes them). Those regiments ignore their brigade from the first order on.
- Different battle files list different regiments.
- Campaign battles always write the group flag (ImportExportUnitData.cs:499), so they are not affected by this path.

**Fix.** Such player regiments are re-attached when their parent is an HQ and they are not under AI control.

**Also found (not changed).**
- Group formation orders only reach three levels below the ordering HQ (`SetGroupFormation`, `PerformActionForGroupForAllUnitsOnLevel`). An army-level formation order therefore never reaches the regiments: army → corps → division → brigade → regiment is four levels.
- A unit whose campaign morale is below the crack level starts the battle "broken" (BattleUnits.cs:11024). Its morale is pinned just above the crack level, so any loss routs it, and routed units are dropped from group orders. The morale tooltip says "Unit started broken".
