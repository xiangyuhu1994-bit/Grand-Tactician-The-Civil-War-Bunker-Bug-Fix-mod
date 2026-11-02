## Artillery features (src/ArtilleryFeatures.cs)

Switches: `Artillery.CapturedGunAmmo` (float, used only when PerGunAmmo is off), `Artillery.PerGunAmmo`, `Artillery.GunDestruction`, `Artillery.GunDestructionChance`.

### How vanilla works
- **Ammo is per regiment.** `Regiment.ammo[3]` holds fractions: 0 = solid shot (ball for infantry), 1 = shell, 2 = canister. It is set to 1 in `Initialize` (Regiment.cs 3894–3907).
  - Each artillery shot subtracts `1 / (standardroundsperunittyp × guns / generalcasualtiesadjustmentartillery / ammoconsumptionfactorart)` (Regiment.cs 10301–10306). So the fraction is already an "average per gun".
  - Resupply: `SetAmmoVariable` (8875). `LevelAmmo` averages ammo across a brigade, weighted by strength (BattleUnits.cs 8756–8812).
  - Firing never stops for lack of ammo. Rate of fire is only multiplied by the "ammo out" factor (0.05).
- **Gun models.** `Artillery` (Artillery.cs) holds only `weapon` and `condition`. `condition` is never set to anything but 0, and `Start()` resets it to 0 (Artillery.cs 42–48), which also wipes the value loaded from a save.
  - A battery's models are `Regiment.gunsprite` (private) and the count is `Regiment.guns`. Each model stands for about `artilleryspritespergun × gunspersprite` real guns (8 × 0.5 = 4 for foot artillery).
  - "Real" guns are `statsguns = ceil((sprites − 1) × gunspersprite)` (`UpdateStatsGuns`, Regiment.cs 26251–26276). They are derived from the crew. Every drop in statsguns is added to `gunslost`.
- **Leaving guns.** `LeaveGuns(howmany)` (26120) re-parents the models to the `LeftGuns` scene object. It is called on rout (14698), on a reattach order (20606, BattleUI.cs 4038), and when crew losses mean fewer models are needed (`RemoveUnitSprite`, 27934). `AdjustGunSprites` (26100) does the same after a save is loaded.
- **Capturing and recrewing.** `BattleUI.GrabLeftCannon` (BattleUI.cs ~5180–5243) does one of two things:
  - An artillery unit that is short of guns grabs them.
  - Otherwise, `BattleUnits.DetachCompany(inf, standardaridetachment, 2)` clones the infantry or cavalry regiment (BattleUnits.cs 12701). The clone keeps the infantry's `ammo` array: {rifle fraction, 1, 1}.

  Either way, `CheckForTakingGuns` (Regiment.cs 20526) later calls `GrabGuns` (26183), which never touches ammo. Both paths already skip guns with `condition != 0`. The AI's `CheckForLeftGuns` (AIBattle.cs 2946) does not check condition, but `GrabLeftCannon` filters again.
- **Gun losses after battle.** The losing side's captured weapons are `gunslost × U(rangefactorweaponscapturedfrom, …to)` (BattleUnits.cs 5913–5916; auto-resolve: Autocalc.cs 3936). These go to the winner's `weaponstock` (ImportExportUnitData.cs ~3419). The loser's stock is never reduced. A campaign battery's gun count is recalculated from crew strength.
- **Saves.** Left guns are saved to `artillery.dat`: weapon, transform, condition, priorparent (ImportExportUnitData.cs 7291–7362). Regiment `ammo`, `guns`, `gunslost` and `statsguns` are saved in the unit data.

### What the mod changes
1. **CapturedGunAmmo** (applies whenever PerGunAmmo is off; set it to 1 to get vanilla behaviour).
   - A postfix on `DetachCompany` runs when an infantry or cavalry unit detaches an artillery crew (targetunittype 2, which only happens from `GrabLeftCannon`). It sets the crew's ammo to {c, c, c}.
   - An existing battery that grabs guns keeps its own ammo, as in vanilla.
2. **PerGunAmmo**
   - Prefixes on `LeaveGuns` and `AdjustGunSprites` store the battery's current ammo on every model that leaves. The store is a `ConditionalWeakTable<Artillery, float[]>`.
   - A postfix on `GrabGuns` sets `ammo = (old × n_old + Σ stored) / (n_old + n_new)`, counted in gun models. Models with nothing stored use CapturedGunAmmo. The infantry's rifle ammo is ignored, because a fresh crew has n_old = 0.
   - A battery that loses guns to casualties keeps its fraction, since the fraction is per gun.
   - Brigade ammo levelling, resupply and the reattach merge are left as vanilla.
3. **GunDestruction**
   - A prefix on `Projectile.DestroyBullet` gives every solid-shot hit (ammotype 0, fired by artillery) on an enemy battery's block a `GunDestructionChance` chance to destroy one real gun.
   - The battery then gets an intact-gun cap: `Cap = statsguns − 1`. A postfix on `UpdateStatsGuns` clamps `statsguns` to the cap and adds the difference to `gunslost`, using vanilla's own bookkeeping. Spare crew can no longer bring a gun back.
   - When the cap needs fewer models, the model at the hit block (block / 3) is moved to the end of the list, set to `condition = 1`, and dropped with `LeaveGuns(1)`. If no guns remain, all models are marked and `LeaveGuns()` runs (the crew becomes "limbered", as on rout). Wrecks therefore stay on the field and can never be recrewed.
   - A `GrabGuns` prefix raises the cap by the guns grabbed. A crew that left its guns starts again from 0.
   - A Start() patch keeps a non-zero `condition`. A `UpdateLeftGuns` postfix removes wrecks from the AI's list.
   - A `SaveBattleResult` prefix hides destroyed guns from the winner's capture roll, and a finalizer restores them. They still count in `gunslost` and in the "guns lost" statistics.
4. **Battle save/load.** Both side tables live only in memory. The mod writes `bugfixmod_artillery.txt` next to `artillery.dat`:
   - Per-gun ammo is saved in the same `GetComponentsInChildren` order that vanilla uses, and read back after `LoadArtillery`.
   - Destroyed and cap counts per battery are saved by name and commander, and read back after `LoadBattleUnitData`.
   - With no sidecar (an older save, or the mod off at save time), left guns use CapturedGunAmmo and caps are lost. This is the least-bad fallback: you get vanilla behaviour, not an error.

### Difficulty, risks, untested assumptions
- **Nothing has been tested in game.** The code compiles against the decompiled DLL only.
- **Campaign gun counts are not reduced.** In the campaign, a battery's guns are a function of its men, so a battery that survives returns with full guns. Fixing that means killing crew or adding a persistent gun field. Both are invasive, so they are left out.
- **Capture roll.** The loser's own weapon stock is never reduced in vanilla. "Lost, not capturable" is therefore implemented only as exclusion from the winner's capture roll.
- **Model swap.** Swapping models inside `gunsprite` before `LeaveGuns(1)` assumes the layout is rebuilt from the list order. `SetRegimentSpritePos` is called straight after, so this should hold. Watch for a model briefly snapping to another block.
- **Sidecar matching.** It relies on left-gun hierarchy order and on unit names staying stable. If the gun count differs, a warning is logged.
- **Guns left while PerGunAmmo was off** carry no stored ammo, so they get CapturedGunAmmo when grabbed.
- **Detachments that merge back.** When a capturing detachment reattaches to its infantry, it is deleted with its `gunslost`. This is vanilla behaviour, and its destroyed-gun record goes with it.
- **Balance.** The default is 0.05 per block hit. At higher values concentrated counter-battery fire with solid shot will destroy guns quickly, because each bouncing ball can hit several blocks. Tune `GunDestructionChance` (0.03–0.05 is probably closer to historical).
