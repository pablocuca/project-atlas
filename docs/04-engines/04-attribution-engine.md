# Attribution Engine

**Status:** Ratified · **Owner:** CTO · **Context:** C3 (Core)

> **This is the product's actual differentiator.** Forecasting is a commodity. Honest attribution
> and disciplined silence are not.

---

## 1. The problem it solves

The FI date moved 41 days. Why?

Without attribution, the only honest answer is "many things changed". With attribution:

```
Δ t_FI = −41 days

  Controllable      −34 d   ├─ Salary increase              −28 d
                            └─ Savings rate change          −6 d
  Structural        −11 d   ├─ SELIC path revision          −7 d
                            └─ IPCA surprise                −4 d
  Stochastic        +4 d    └─ Equity marks                 +4 d   ⟵ never alerts
  Residual           0 d
```

**Only the first two categories may reach the user.** The third consumes a variance budget and is
inspectable on demand, never pushed. This is [ADR-0009](../03-architecture/adr/ADR-0009-attribution-gated-alerting.md),
and it is the difference between Mission Control and an anxiety machine.

---

## 2. Method

### 2.1 The naive approach and why it fails

Changing one variable at a time and measuring the effect ("one-at-a-time" sensitivity) fails because
drivers **interact non-linearly** (hotspot H6). A salary increase and a spending increase do not
simply add — the tax bracket, the savings rate, and the contribution schedule all interact. One-at-a-time
attributions will not sum to the total, and the residual will be large and arbitrary.

### 2.2 Shapley value decomposition

Atlas attributes using **Shapley values**, the unique allocation satisfying efficiency (contributions
sum exactly to the total), symmetry, dummy (a driver with no effect gets zero), and additivity.

```
For drivers D = {d₁ … dₙ}, snapshots S₀ (before) and S₁ (after):

φ(dᵢ) = Σ_{T ⊆ D∖{dᵢ}}  [ |T|!(n−|T|−1)! / n! ] · [ v(T ∪ {dᵢ}) − v(T) ]

where v(T) = metric evaluated on the hybrid snapshot taking drivers in T from S₁
             and all others from S₀
```

Efficiency is exactly INV-130: `Σ φ(dᵢ) = v(D) − v(∅) = total delta`. The mathematical property and
the domain invariant are the same statement, which is why this method was chosen.

### 2.3 Making it computable

Exact Shapley requires `2ⁿ` evaluations. With ~12 drivers that is 4,096 forecast runs — far outside
the compute budget. Three techniques bring it inside:

| Technique | Effect |
|---|---|
| **Analytic sensitivities first** | `∂t_FI/∂x` is already cached on the artifact. First-order attribution is free and handles most cases |
| **Monte Carlo Shapley** | Sample permutations rather than enumerate. ~200 samples gives < 1% error |
| **Driver grouping** | Attribute to ~8 groups first; drill into a group only on demand |

The engine escalates: analytic → grouped sampled Shapley → full sampled Shapley, stopping as soon as
the residual invariant (≤ 2%, INV-131) is satisfied. Most days stop at step one.

### 2.4 Residual discipline

If `|residual| > 2%` of the total delta, the attribution is **rejected** and an *engineering* alert
is raised (never a user alert). A wrong attribution is worse than none, because it would name the
wrong cause with full confidence — and the user would act on it.

---

## 3. Driver taxonomy

| Driver | Class | Source |
|---|---|---|
| Savings rate change | Controllable | Cashflow |
| Discretionary spending change | Controllable | Cashflow |
| Essential spending change | Controllable* | Cashflow |
| Income action (raise, job change, side income) | Controllable | Human Capital |
| Contribution deviation from policy | Controllable | Goals & Policy |
| Allocation change | Controllable | Policy |
| Debt action (extra amortisation, refinance) | Controllable | Liabilities |
| Goal change (amount, date, priority) | Controllable | Goals |
| SELIC / CDI path revision | Structural | Market Data |
| IPCA surprise | Structural | Market Data |
| Personal inflation divergence | Structural | Cashflow |
| Tax law change | Structural | Taxation |
| Mortgage rate reset / indexation | Structural | Liabilities |
| Employment risk reassessment | Structural | Human Capital |
| Equity / FII marks | **Stochastic** | Valuation |
| FX marks | **Stochastic** | Valuation |
| Fixed income mark-to-market | **Stochastic** | Valuation |
| Model version change | `ModelChange` | Forecast |
| Assumption set change | `ModelChange` | Forecast |
| Data correction (late-arriving) | `Restatement` | Ledger |

\* Essential spending is classed Controllable but flagged `LowElasticity` — the user controls it in
principle, over a longer horizon, and the UI must not imply it is a quick lever.

### The two special classes

`ModelChange` and `Restatement` exist because of bitemporality and versioning, and they matter more
than their obscurity suggests:

- **`ModelChange`** — the number moved because *we* changed, not because the user's life did.
  Presented in a separate channel with an explicit label (BR-204). Never counted in Controllable
  Drift.
- **`Restatement`** — the number moved because a fact arrived late and history was corrected. Only
  answerable because the ledger is bitemporal (ADR-0002). Shown as "we learned something new about
  the past", which is a categorically different message from "something changed".

Most systems silently fold both into ordinary change. That is precisely how a change feed becomes
untrustworthy.

---

## 4. The Signal Gate

```
                      Delta + Attribution
                              │
              ┌───────────────▼───────────────┐
              │ 1. CLASS FILTER               │  Controllable or Structural present?
              │    Stochastic-only → SUPPRESS │  ← structurally enforced by input type
              └───────────────┬───────────────┘
              ┌───────────────▼───────────────┐
              │ 2. MATERIALITY                │  ≥ 30 FI-days OR ≥ 1.0pp P(FI)?
              └───────────────┬───────────────┘
              ┌───────────────▼───────────────┐
              │ 3. SIGNIFICANCE               │  > 2σ of trailing 90-day noise band?
              └───────────────┬───────────────┘
              ┌───────────────▼───────────────┐
              │ 4. DEDUPLICATION              │  same driver + direction within 14d? → merge
              └───────────────┬───────────────┘
                              ▼
                        ChangeEvent  →  user
```

### On the significance gate
The noise band is the empirical distribution of *stochastic-only* deltas over the trailing 90 days.
This is what prevents a genuine but small structural change from being announced during a volatile
period when it is indistinguishable from noise — and, equally, allows a modest change to surface
during a calm period when it genuinely stands out. The threshold adapts to conditions rather than
being fixed.

### Suppression is not deletion
Suppressed deltas are stored with full attribution and are queryable (BR-214). The variance view
shows everything. **Nothing is hidden; things are simply not shouted about** — the distinction is
the entire product philosophy in one sentence.

### Gate health as a product metric
`atlas_signal_gate_decisions_total{decision}` is monitored. A suppress:emit ratio trending toward
1:1 means the gate is failing and the product is becoming noisy — reviewed quarterly as a product
health indicator, not just an ops metric.

---

## 5. Periods and framing

The founding brief asks "what changed today / this month / this year?" — but the correct answer
differs by period, and using the same machinery for all three would be wrong.

| Period | Comparison | Dominant class | Framing |
|---|---|---|---|
| **Day** | Prior artifact | Stochastic | Almost always silence. Daily deltas are noise by construction |
| **Month** | Month-start artifact | Mixed | Change feed of gated events |
| **Quarter** | Quarter-start | Controllable + Structural | Trend and driver ranking |
| **Year** | Year-start | Controllable | **Controllable Drift** — what the user actually did |
| **Since inception** | First artifact | All | Full history, `ModelChange` annotated |

**The annual view is the most valuable and the least common.** Over a year, stochastic contributions
substantially cancel and controllable contributions accumulate — so the annual Controllable Drift is
the cleanest available measure of whether the user is *actually improving*. It is the honest answer
to "am I getting better?", and it is a metric no consumer product currently shows.

---

## 6. Interaction with reliability and advice

| Consumer | What it takes |
|---|---|
| **Reliability (C13)** | `ChangeEvent`s feed SLI evaluation and incident declaration |
| **Advisory (C5)** | `DriverRanking` indicates where leverage is, seeding the policy space |
| **Progression (C14)** | Nothing. Progression cannot see attribution — it would leak market data (MR-8) |
| **Narrative (C15)** | A `FactSet` derived from the attribution, never the attribution object itself |

---

## 7. Failure modes

| Failure | Response |
|---|---|
| Residual > 2% | Reject; engineering alert; no user output |
| `ModelVersion` mismatch between artifacts | Refuse (BR-203); label the discontinuity |
| Shapley sampling not converged | Escalate sample count; if still failing, reject |
| Snapshot missing for the comparison point | Attribute against the nearest prior; **disclose the gap** |
| Driver cannot be classified | Treat as `Structural` (conservative — surfaces rather than hides) and raise an engineering alert |

Note the direction of the last one: an unclassifiable driver defaults to *visible*, not *silent*.
Failing toward silence would mean the gate could quietly swallow a real signal, which is the one
failure mode this engine must not have.

---

**See also:** [Forecast Engine](02-forecast-engine.md) · [Financial Reliability Model](08-financial-reliability-model.md) · [ADR-0009](../03-architecture/adr/ADR-0009-attribution-gated-alerting.md)
