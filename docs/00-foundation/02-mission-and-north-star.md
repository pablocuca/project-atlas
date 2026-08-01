# Mission & North Star

**Status:** Ratified · **Owner:** CTO / Product · **Last reviewed:** 2026-08-01

---

## 1. Mission

> Continuously determine whether the user remains on the best available path to Financial
> Independence under uncertainty — and when they are not, explain precisely why, by how much,
> and what the alternatives cost.

## 2. Why the original mission statement was not yet buildable

The founding question — *"am I still on the highest probability path?"* — is **unfalsifiable as
written**. "Highest probability path" is only meaningful relative to an enumerated set of
alternatives. Without one, the system either answers "yes" trivially or answers noise.

Atlas therefore formalises the question as an **optimisation over a defined policy space**:

> Let **Π** be the set of feasible policies the user could adopt today.
> Let **π₀ ∈ Π** be the policy currently in force (the status quo).
> Let **U(π)** be the outcome distribution of policy π.
>
> The system answers: **is π₀ within the non-dominated set of Π under the user's stated
> preferences — and if not, which π ∈ Π dominate it, and by how much?**

This is buildable, testable, and honest. It also reframes the product: Atlas is a
**counterfactual engine**, not a monitor. See [Recommendation Engine](../04-engines/05-recommendation-engine.md)
for the definition of Π.

## 3. Financial Independence, defined

FI is not "25× expenses". That heuristic ignores taxes, sequencing, non-stationary spending, and
mortality. Atlas defines it precisely:

> **Financial Independence is reached at the earliest time _t_ at which the probability of
> sustaining the user's Essential + Committed Discretionary spending, in real (inflation-adjusted)
> terms, net of all Brazilian taxes, from portfolio and non-labour income alone, through the
> planning horizon _H_, is at least the user's Confidence Target _c_.**

Formally, FI is reached at:

```
t_FI = min { t : P( no ruin over [t, H] | π, twin_state_t ) ≥ c }
```

Where:

| Symbol | Meaning | Default |
|---|---|---|
| `H` | Planning horizon — age at which the plan must still hold | Age 95 (survival-adjusted) |
| `c` | Confidence Target — user's required success probability | 85% |
| *ruin* | Real, after-tax portfolio value reaches zero before `H` while spending floor unmet | — |
| *spending floor* | Essential + Committed Discretionary, non-stationary, per [Cashflow context](../02-domain/02-bounded-contexts.md) | — |

**Three consequences that most tools get wrong and Atlas must not:**

1. FI is a **date distribution**, never a date. Atlas always reports P10 / P50 / P90.
2. FI is defined **after tax**. A pre-tax FI number is off by years in Brazil.
3. FI is **conditional on a policy**. Changing contribution rate changes `t_FI`. The headline
   figure is always `t_FI | π₀` — status quo — with alternatives shown as deltas.

## 4. The North Star Metric

**`P(FI by Target Date)`** — the probability, under the status-quo policy and the current twin
state, that Financial Independence is reached on or before the user's declared Target Date.

It is the North Star because it is the only metric that:

- moves for every meaningful reason (income, spending, markets, taxes, rates, life events),
- moves for *no* meaningless reason once attribution-gated,
- is directly falsifiable over time by the [Calibration subsystem](../04-engines/06-calibration-and-scoring.md),
- and is comprehensible in one sentence.

### 4.1 Supporting metrics (the instrument panel)

North Star metrics fail alone. Atlas pairs it with four counterweights:

| Metric | Definition | Why it exists |
|---|---|---|
| **FI Date Band** | P10 / P50 / P90 of `t_FI` | Guards against reading a single probability as certainty |
| **Freedom Ratio** | After-tax non-labour income ÷ Essential spending (trailing 12m) | A *realised*, non-simulated fact. Immune to model error |
| **Forecast Reliability** | Rolling calibration score of the system's own past forecasts | Guards against a confident, wrong model |
| **Controllable Drift** | 12-month sum of ΔFI-days attributable to *decisions only* | Isolates what the user actually did from what happened to them |

> **Design law.** The North Star may never be displayed without the Forecast Reliability figure
> available within one interaction. A probability from an uncalibrated model is a false precision.

## 5. What the system must answer, and where

Every question in the founding brief is assigned an owning subsystem. A question with no owner is
a specification gap.

| Question | Answered by | Surface |
|---|---|---|
| How healthy is my financial life? | [Financial Reliability Model](../04-engines/08-financial-reliability-model.md) | Health Score + SLO panel |
| Am I improving or getting worse? | Attribution Engine (12m Controllable Drift) | Trend rail |
| How many years until FI? | Forecast Engine | FI Date Band |
| What changed since yesterday / month / year? | Attribution Engine | Change Feed, period-scoped |
| Which variable contributes most / hurts most? | Attribution Engine (Shapley decomposition) | Driver ranking |
| What should I ignore? | Signal Gate — noise-classified deltas | Suppressed by default, inspectable |
| What deserves attention? | Reliability Model — SLO breach + burn rate | Incident list |
| What should I do next? | Recommendation Engine | Ranked Options |
| What if nothing changes? | Simulation Engine — status-quo path | Baseline projection |
| What under other futures? | Simulation Engine — scenario set | Scenario compare |

## 6. Anti-metrics — what Atlas will never optimise for

Recording these is as important as recording the North Star, because a system optimises for what
it measures whether or not you intended it to.

- ❌ Session count, session length, daily active use — **success is fewer sessions**
- ❌ Portfolio value — an outcome the user does not control day to day
- ❌ Daily or weekly return — pure noise at this horizon
- ❌ Number of transactions categorised — an input cost, not a benefit
- ❌ Streak length as a headline — see [Gamification anti-Goodhart constraints](../01-product/06-gamification-strategy.md)

---

**See also:** [Product Philosophy](03-product-philosophy.md) · [Attribution Engine](../04-engines/04-attribution-engine.md) · [Calibration & Scoring](../04-engines/06-calibration-and-scoring.md)
