# Forecast Engine

**Status:** Ratified · **Owner:** CTO / AI Architect · **Context:** C2 (Core)

> Produces the North Star. Its integrity is the product's integrity.

---

## 1. Contract

```
Forecast : (TwinSnapshot, ModelVersion, AssumptionSet, Seed) → ForecastArtifact
```

**Pure.** Same inputs ⇒ bit-identical output, on any machine, in any year (BR-302). No clock, no
network, no ambient state, no unordered parallel reduction. This is enforced by a CI gate, not by
discipline.

---

## 2. What is simulated

For each path `p ∈ 1..N`, month `m ∈ 1..H×12`, the engine evolves:

```
1. Draw regime rₘ                 (Markov transition from rₘ₋₁)
2. Draw asset returns             (block bootstrap or regime-conditional, per model)
3. Draw inflation πₘ              (jointly with regime — critically, NOT independent)
4. Draw income shock              (employment hazard; correlated with equity via marketBeta)
5. Evolve human capital           (trajectory, employment state)
6. Evolve spending                (non-stationary process + personal inflation + life events)
7. Service liabilities            (amortisation, indexation, rate resets)
8. Apply policy π                 (contributions, rebalancing bands, debt strategy)
9. Compute taxes                  (ITaxJurisdiction — come-cotas, disposals, income)
10. Update portfolio              (post-tax, post-fee)
11. Test FI condition             (real, after-tax, sustainable through H at confidence c)
12. Test ruin condition
```

**Order matters and is fixed.** Taxes are computed after returns and before the FI test, because a
pre-tax FI test is wrong by years in Brazil (Law 12). Inflation is drawn jointly with regime,
because inflation/return independence is the second-most-damaging simplification after
return independence.

---

## 3. Return models

Per [ADR-0007](../03-architecture/adr/ADR-0007-return-model.md), an **ensemble**:

### 3.1 Stationary block bootstrap
```
Block length L ~ Geometric(p), E[L] ≈ 24 months
Resample contiguous blocks from the joint historical return matrix
(equities BR/global, FII, fixed income, inflation, FX) — jointly, preserving cross-asset structure
```
Preserves autocorrelation, volatility clustering, and realistic joint drawdowns without assuming a
distributional form. Blocks are drawn **jointly across assets**, not per-asset — sampling assets
independently would destroy exactly the correlation structure that causes ruin.

### 3.2 Regime-switching model
```
States: Expansion | Stress | Stagflation
Transition matrix P estimated from historical regime classification
Per-state: μ, Σ (returns), and inflation process parameters
```
Supplies futures the historical sample under-represents, and gives named scenarios the user can
reason about. Particularly important for BRL, where the inflation/rates block dominates real
outcomes.

### 3.3 Ensemble and model uncertainty
Both families run; the artifact reports the pooled distribution **and the spread between families**
as `modelUncertainty` (INV-122). That spread is the most credible available estimate of how much the
model itself could be wrong — and reporting it is what turns "83%" from a claim into a measurement.

### 3.4 Baseline (comparison only)
i.i.d. lognormal is retained **solely** as a labelled comparison, never as a primary engine
(BR-304). Its presence lets Atlas show explicitly how much the naive model would have
under-stated risk — a genuinely useful piece of user education.

---

## 4. Brazilian specifics

| Element | Treatment |
|---|---|
| Inflation | IPCA process, regime-linked, **plus** the user's own personal inflation divergence |
| Real returns | All outputs deflated; nominal figures must be explicitly labelled (BR-307) |
| Fixed income | CDI/SELIC path modelled jointly with inflation, not as a constant spread |
| Indexed instruments | IPCA+ instruments correctly track the simulated inflation path |
| FX | USD/BRL modelled for foreign holdings, correlated with the local stress regime |
| Fees | *Taxa de administração*, custody, brokerage — deducted per period, not annualised away |
| Tax | Every period routed through `ITaxJurisdiction` (BR-306) |

---

## 5. Human capital and the correlated-shock model

```
Employment state ∈ {Employed, Unemployed, Retired}
P(job loss in month m | Expansion)   = base hazard
P(job loss in month m | Stress)      = base hazard × stressMultiplier   (default 3×)
On job loss: severance + FGTS inflow, search duration ~ Lognormal,
             re-employment income ~ prior × haircut distribution
```

This is where the correlated shock lives: **job loss probability rises in exactly the regimes where
the portfolio is depressed**. Modelling these as independent is the single most common and most
consequential error in retirement simulation, and it flatters the result precisely in the scenarios
that determine ruin.

`marketBeta` is mandatory (INV-080); zero must be a deliberate, recorded choice.

---

## 6. Spending: non-stationary by construction

Spending is **not** a constant deflated by IPCA. The model includes:

- **Category-level processes** — recurring, seasonal, episodic, trending, each fitted separately.
- **Personal inflation** — the user's own basket, which diverges from IPCA. The divergence is itself
  a reported driver.
- **Life-event step changes** — dependents, education, healthcare, housing, each with its own
  timing distribution.
- **Age-related drift** — the empirical retirement spending smile (early activity, mid-life decline,
  late healthcare rise), applied as a modelled curve rather than a flat assumption.
- **Essential vs discretionary floor** — only the floor must be covered for FI; discretionary
  spending flexes in the model, which is what makes the FI test meaningful rather than punitive.

---

## 7. Outputs

```
ForecastArtifact.outcomes
├─ fiDateDistribution        full CDF + P10/P25/P50/P75/P90
├─ pFIByTargetDate           THE NORTH STAR, with its confidence band
├─ ruinProbability           by horizon decade
├─ terminalWealthDistribution
├─ goalAchievementProbabilities[]
├─ pathSummaries             worst decile, median, best decile — full trajectories retained
├─ sensitivities             ∂t_FI/∂x for each policy lever  ← feeds the Materiality Gate
└─ modelUncertainty
    ├─ ensembleSpread        disagreement between model families
    ├─ parameterSensitivity[]
    └─ monteCarloStdError    convergence quality of this run
```

`sensitivities` is computed here, once, and cached on the artifact — it is what makes the
Materiality Gate free ([Digital Twin §4](01-digital-twin.md)) and what seeds the Advisory policy
space.

---

## 8. Convergence and compute budget

| Parameter | Default | Rationale |
|---|---|---|
| Paths per model family | 20,000 | Monte Carlo standard error on `P(FI)` ≈ 0.35pp |
| Model families | 2 (+1 baseline) | Ensemble |
| Total paths | ~50,000 | |
| Horizon | to age 95, monthly | ~660 steps |
| Target runtime | ≤ 90 s on 2 vCPU | Fits the Container Apps Job budget |

**Convergence is checked, not assumed** (BR-309): the run computes its own standard error and is
**rejected** if it exceeds the target. An under-converged forecast presented as precise is a lie
with extra steps.

Performance techniques: `Span<T>` and struct-based path state, no per-step allocation, SIMD where
the return draw permits, deterministic parallelism (each path gets a **counter-based RNG stream
derived from the seed and path index** — so results are identical regardless of thread scheduling,
core count, or execution order). That last point is what makes parallelism compatible with BR-302.

---

## 9. Assumption set

```
AssumptionSet (hashed into artifact identity)
├─ historicalWindow          which data, which period
├─ regimeParameters          transition matrix, per-state moments
├─ blockLengthDistribution
├─ feeAssumptions
├─ mortalityTable            IBGE, survival-adjusted horizon
├─ employmentHazardParams
├─ spendingSmileCurve
└─ rulesetVersion            tax law version (ADR-0017)
```

Changing any of these changes the artifact hash, so a comparison across differing assumption sets is
**structurally prevented** rather than merely discouraged. Assumption changes are labelled in trends
exactly like model changes (BR-204).

---

## 10. Degradation

| Condition | Behaviour |
|---|---|
| Stale marks | Compute; label `Degraded`; state which marks and how stale |
| Coverage < 95% | Compute; label `Degraded`; state the unmodelled fraction |
| Unmodelled assets present | Exclude from distribution; report separately (BR-308, NG-10) |
| Convergence failed | **Reject.** Retry with more paths; never present |
| Tax ambiguity | Compute conservative branch; flag `TaxAmbiguity` (BR-408) |

Consistent with the failure philosophy: **degrade and disclose, but never present an un-converged
or incomplete result as if it were sound.**

---

**See also:** [Simulation Engine](03-simulation-engine.md) · [Calibration & Scoring](06-calibration-and-scoring.md) · [ADR-0007](../03-architecture/adr/ADR-0007-return-model.md)
