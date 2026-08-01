# Simulation Engine

**Status:** Ratified · **Owner:** CTO / AI Architect · **Context:** C2 (Core)

> The Forecast Engine answers *"what happens under the status quo?"*. The Simulation Engine answers
> *"what happens under a different world, or a different choice?"* — and it is what makes Atlas a
> counterfactual engine rather than a monitor.

---

## 1. Scenario algebra

A **Scenario** is a named, parameterised, composable deviation from the base assumption set.

```
Scenario := (name, perturbations[], probability?, provenance)
Perturbation := (target, operation, magnitude, timing, duration)

target     ∈ { returns.equity, inflation, rates.selic, income.primary,
               spending.essential, spending.discretionary, employment.state,
               liability.rate, humanCapital.trajectory, tax.ruleset, … }
operation  ∈ { Shift, Scale, Replace, Shock, RegimeForce }
timing     ∈ { Immediate, AtDate(d), AtAge(a), Random(distribution) }
duration   ∈ { Permanent, Months(n), UntilRecovery }
```

Scenarios compose:

```
Stress2008        = RegimeForce(Stress, 18mo) ∘ Shock(returns.equity, −45%)
JobLoss           = Shock(employment.state, Unemployed, Random(hazard)) ∘ Months(9)
Stagflation       = RegimeForce(Stagflation, 36mo) ∘ Shift(inflation, +6pp)
PerfectStorm      = Stress2008 ∘ JobLoss           ← correlated, NOT independent
```

**Composition must respect correlation.** `PerfectStorm` is not "run both independently" — job loss
during a crash is *more likely* than the product of the marginals, and that is the whole point of
modelling it. Composition applies the joint structure from the regime model, never naive
independence.

---

## 2. The standard scenario library

| Scenario | Definition | Question it answers |
|---|---|---|
| `StatusQuo` | No perturbation | The baseline. What the North Star reports |
| `JobLoss` | 9-month unemployment, severance + FGTS, re-entry haircut | Can I survive losing my job? |
| `JobLossInCrash` | JobLoss ∘ Stress regime, correlated | **The real tail risk** |
| `Stress2008` | Forced stress regime, 18 months | A severe but historically observed crash |
| `Stagflation` | High inflation + low real returns, 36 months | The scenario that most damages a BRL FI plan |
| `RateShock` | SELIC +8pp sustained | Mortgage reset and fixed income repricing |
| `HealthEvent` | Step increase in essential spending, permanent | Long-term care or chronic condition |
| `DependentAdded` | Spending step + horizon extension | A child, planned or otherwise |
| `EarlyRetirement` | Target date −5 years | What would it take? |
| `PropertyPurchase` | Liquidity draw + new liability | Housing decision |
| `IncomeStep` | Permanent income increase | Career move |
| `TaxRegimeChange` | Alternative tax ruleset | Legislative risk |
| `LongevityTail` | Horizon to age 105 | Outliving the plan |

Each is defined as **data**, versioned with the assumption set, and hashed into artifact identity.

---

## 3. Counterfactuals vs scenarios — a distinction that matters

| | **Scenario** | **Counterfactual** |
|---|---|---|
| Perturbs | The *world* | The user's *policy* |
| Question | "What if the world does X?" | "What if I do Y?" |
| Controllable | No | Yes |
| Consumed by | Risk view, stress testing | **Advisory** — option evaluation |
| Example | Equity crash | Increase savings rate by 3pp |

```
Counterfactual := (baselineSnapshot, policyDelta, scenarioSet)
```

**Critical rule (INV-150):** a counterfactual runs on the **same `TwinSnapshot`** as the baseline.
Comparing an option against a differently-dated snapshot would conflate the option's effect with
elapsed time and market movement — producing option rankings that are silently wrong. This is
enforced by the type: `Counterfactual` cannot be constructed with two snapshots.

Counterfactuals are also evaluated **across the scenario set**, not only under `StatusQuo`. An option
that improves the median while worsening the `JobLossInCrash` tail is a materially different
proposition, and the Advisory layer must be able to say so.

---

## 4. Execution model

```
                          Simulation request
                                  │
                   ┌──────────────▼──────────────┐
                   │ Resolve: snapshot, model,   │
                   │ assumptions, scenario, seed │
                   └──────────────┬──────────────┘
                   ┌──────────────▼──────────────┐
                   │ Cache lookup by content hash│──── hit ──▶ return existing artifact
                   └──────────────┬──────────────┘
                             miss │
                   ┌──────────────▼──────────────┐
                   │ Enqueue to atlas-sim Job    │
                   └──────────────┬──────────────┘
                   ┌──────────────▼──────────────┐
                   │ Parallel path execution     │
                   │ counter-based RNG per path  │
                   └──────────────┬──────────────┘
                   ┌──────────────▼──────────────┐
                   │ Aggregate → verify          │
                   │ convergence → store         │
                   └─────────────────────────────┘
```

### Deterministic parallelism
Each path derives its random stream from `(seed, pathIndex)` via a **counter-based RNG**
(Philox/Threefry style) rather than from a shared sequential generator. Consequences:

- Results are identical regardless of thread count, scheduling, or execution order.
- Paths can be recomputed individually without replaying the sequence.
- Parallelism is compatible with BR-302 (bit-identical replay), which a shared-state PRNG would
  break immediately.

This is a small implementation detail with outsized importance: it is the difference between a
reproducible engine and one that *almost* reproduces.

### Caching
Every simulation is content-addressed like a forecast. Advisory evaluating five options against six
scenarios is 30 runs — but across days, most are cache hits, because the snapshot only changes when
something material happens. The cache is what makes ranked-options advice affordable inside the
compute budget.

---

## 5. Compute budget

| Workload | Paths | Frequency | Runtime target |
|---|---|---|---|
| Daily status quo | 50,000 | Daily | ≤ 90 s |
| Materiality-triggered | 50,000 | ~5×/month | ≤ 90 s |
| Scenario sweep (13 scenarios) | 20,000 each | Weekly | ≤ 8 min |
| Advisory counterfactuals | 20,000 × options × scenarios | On demand, cached | ≤ 5 min |
| Attribution hybrids | 5,000 | Per attribution | ≤ 30 s |
| **Monthly total** | | | **~30 Job-minutes** |

At ~30 Job-minutes/month on 2 vCPU, this fits inside US$1.50–4.00 —
consistent with [Infrastructure §3](../03-architecture/08-infrastructure.md).

Reduced path counts for scenarios and attribution hybrids are deliberate: those outputs feed
*comparisons*, where common random numbers (the same seed across variants) cancel most of the Monte
Carlo error. Comparing two runs with identical seeds is far more precise than comparing their
absolute values — a standard variance-reduction technique that here saves roughly an order of
magnitude of compute.

---

## 6. Variance reduction

| Technique | Application |
|---|---|
| **Common random numbers** | Same seed across counterfactuals — makes small option differences detectable |
| **Antithetic variates** | Paired opposite draws; reduces variance in symmetric components |
| **Stratified sampling** | Over-sample the tail regions that determine ruin probability |
| **Control variates** | Use the analytically tractable deterministic-return case as a control |

Common random numbers matter most: without them, distinguishing a 12-day FI improvement from Monte
Carlo noise would require an order of magnitude more paths. With them, the *difference* between two
runs is far more precisely estimated than either run's absolute value — which is exactly what option
ranking needs.

---

## 7. Outputs

```
SimulationResult
├─ artifact              ForecastArtifact (immutable, content-addressed)
├─ scenarioRef
├─ vsBaseline            when a comparison, computed with common random numbers
│   ├─ fiDateDelta       {p10, p50, p90} — a distribution, never a point
│   ├─ pFIDelta
│   ├─ ruinProbabilityDelta
│   └─ significanceOfDelta   is this distinguishable from Monte Carlo noise?
└─ tailBehaviour
    ├─ worstDecilePaths
    ├─ timeToRuinDistribution
    └─ recoveryProfile
```

`significanceOfDelta` is mandatory on comparisons. An option whose effect is within Monte Carlo
noise **must be presented as "no detectable difference"**, never as a precise small improvement.
Ranking options by differences the engine cannot actually resolve would be false precision at the
exact point where the user is making a decision.

---

## 8. Failure modes

| Failure | Response |
|---|---|
| Convergence not achieved | Increase paths, retry once; then reject. Never present |
| Job timeout | Fan out into parallel Job replicas (config-only change) |
| Scenario composition produces an incoherent world (e.g. negative real spending) | Reject at composition, not at execution |
| Counterfactual snapshot mismatch | Type-level impossible (INV-150) |
| Delta within noise | Report "no detectable difference" — **never** a spurious precise value |

---

**See also:** [Forecast Engine](02-forecast-engine.md) · [Recommendation Engine](05-recommendation-engine.md) · [Digital Twin](01-digital-twin.md)
