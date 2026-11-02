## Men leaving the army (src/Manpower.cs)

**How it works in vanilla.** Each state has a recruit pool = potential − men recruited, and every man recruited also removes 5 people from its towns' workforce. Only disbanding a unit gives men back (its current strength). Deserters (UpdateCampaignUnitStats ~6245) move from strength to `losses[2]`; men who don't re-enlist when contracts expire (CheckContractExpiries ~4600) simply vanish (their weapons go back to stock). Neither returns anyone to the workforce or the pool, and no deserter ever comes back.

### 1. Deserters handed to the enemy as prisoners – `DesertersNotPrisoners`
**Bug.** `losses[2]` holds both campaign desertions and men missing in battle. After every battle `WriteBattleResultToMonument` (~3175) sends each involved unit's whole `losses[2]` to the enemy's POW camps (or paroles it) and zeroes it; `UpdateGroupStrength` (~11388) then restores it from the smoothed `missingavg`, so roughly the same deserters are handed over again after the next battle. Prisoner counts and battle reports are inflated, and with paroles fixed (PardonAddsRecruits) the duplicates would even turn into recruits.
**Fix.** The mod records each unit's `losses[2]` at every campaign update outside battle. Around the battle result each unit's `losses[2]` is cut to what it gained since (the battle's own missing) and the desertion tally is restored afterwards. Tactical battles report only wounded and captured back to the campaign, so for them nothing in `losses[2]` counts as captured. Just after loading, before the first update, the whole tally is treated as desertions.

### 1b. The per-state Deserters figure balloons – `DeserterStatistic`
**Bug.** `Nation.deserters` (shown per state in the recruiting list) grows only at the end of a battle: `Autocalc.FinishLandBattle` (~4058) and `BattleUnits.SaveBattleResult` (~5964) add each of the player's units' whole `losses[2]` – its running desertion tally, which vanilla keeps restoring (see above). Every battle adds all of a unit's past deserters again, while desertions between battles are never counted as they happen.
**Fix.** Those battle-end additions are undone, and deserters are counted when they desert (player's units only, as in vanilla). Figures already in a save stay inflated.

### 2. Departing men go home; some deserters come back – `DesertersGoHome`, `DeserterReturnShare`, `DeserterReturnRate`
**Fix/feature.**
- Men leaving at contract expiry return to their home state: its recruited count drops by that many, restoring workforce and recruit pool.
- Of each update's deserters, `DeserterReturnShare` (default 0.35) are returned to the army later; the rest go home at once. Historical guide: the Union Provost Marshal General's bureau arrested 76,526 deserters against roughly 200,000 recorded Union desertions (estimates run to 279,000), and both sides ran amnesties in 1863–65. Confederate figures (about 104,000 deserters) don't record returns, so the same share is used.
- Returning deserters wait in a pool per state and side and come back at `DeserterReturnRate` per year (default 3: on average about four months away). They fill under-strength infantry and cavalry units from their state, drawing rifles from stock where available; if no unit has room they go home.
- The pool is saved with each campaign save as `bugfixmod_absent.txt`. Older saves start with an empty pool.
