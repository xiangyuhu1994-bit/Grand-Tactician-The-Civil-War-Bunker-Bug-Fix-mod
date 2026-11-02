## Trade and import pricing (src/EconomyFixes.cs)

**How trade works.** Each trade hub (IIP) pulls goods from its connected hubs every economy update. A good's attractiveness on a link is the price gap `relprice(here) − relprice(there) × tariff markup / trade-deal factor` (with ±10 % noise, scaled by the other hub's stock); the link's volume is shared between goods by attractiveness³ and limited by transport capacity and distance. Trade between two sides needs a trade agreement (set per side in the scenario's nations.dat and never changed in play). In the main scenario the Union and the Confederacy each have agreements with Britain, France, Spain, Mexico, Sea Trade and the Neutral States, but not with each other, so no trade crosses the front at all (the 10 % enemy-trade factor never applies).
- **Tariffs:** the markup is `1 + cap × slider`. The cap is set by the scenario and policies (Tariff Act 0.5, also −0.05 European relations once; Free Trade 0.3 for the Confederacy). Revenue is the same rate on the value of imports only; exports are untaxed.
- **Trade Deals project (id 121):** the trade-deal factor is `1 + 0.045 × level` (config label "project send envoys: increase in tradevolume" – misnamed). It applies to every link of the side's hubs, domestic included, so it raises domestic as well as foreign trade.
- **Send Envoys project (id 103):** only +0.05 relations per level.
- Two diplomacy-subsidy settings ("improved prices factor" and "intervention level per year of full diplomacy subsidies") are read but never used.

### 1. Tariff markup leaks onto domestic trade – `TariffPerConnection`
**Bug.** In `IIP.UpdateEconomy` (~941) the markup is set when the loop meets the hub's first foreign link and never reset, so every later link in that hub's list – domestic ones too – is treated as an import when ranking goods. No revenue is collected on them, but domestic flow out of port and border hubs is dampened, depending on link order.
**Fix.** A transpiler replaces the markup in the price-gap formula with a per-link value: 1 for same-side links, `1 + cap × slider` for foreign ones.

### 2. Union import prices follow Confederate relations – `ImportPriceRelationsPerSide`
**Bug.** `Weapon.GetProductionFigures` (~5280) computes the relations term for imported weapons as `(P(Britain) + P(France) − P(Spain)) / 3` in both branches of an `alliance == 1` check. These are intervention probabilities – Britain/France leaning Confederate, Spain leaning Union – so the Union got the Confederate figure: Europe warming to the Confederacy made Union imports cheaper too. The term maps 0 → 1.65× and 1 → 0.75× cost.
**Fix.** The Union gets the mirror image: Britain's and France's lean toward the Union minus Spain's lean toward the Confederacy, on the same chapter-threshold scale as the intervention probabilities. The Confederate figure is unchanged.

### Reference (not changed)
- **Workforce:** a map where towns add √(population) and companies subtract √(workers); blurred, floor 15 %, doubled by `workforceleverage` and capped at 1. Company output scales with it (slaves count as full workforce for their share). Town wealth is multiplied by 1.6 at zero available workforce and 0.9 at full, so labour shortages raise wealth. Each man a state has ever recruited (dead and deserted included) removes 5 people from its towns' population for this purpose.
- **Population:** follows the scenario's census curve (historical immigration is built in), plus a workforce term `(1/√availability − 1.2) × 0.05` per year – up to about +4 %/yr where labour is tight, about −1 %/yr where it is slack. People are created or removed, not moved between states. Some policies multiply state populations once. There is no separate immigration mechanic.
- **Relations** affect only intervention (deterministic once the "probability" reaches 100 %; scenarios 001/002 only) and import prices.
