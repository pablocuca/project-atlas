# Recommendation Engine

**Status:** Ratified · **Owner:** CTO / Product · **Context:** C5 (Core)

> Answers *"what should I do next?"* — the hardest question in the brief — without ever issuing an
> instruction. Per [ADR-0022](../03-architecture/adr/ADR-0022-advice-posture.md): **ranked options
> with quantified tradeoffs and disclosed unknowns.**

---

## 1. The Policy Space

The Mission document formalises the founding question as an optimisation over a defined policy
space. This is that space.

```
Policy π = (
  savingsRate,            targetAllocation,      contributionSchedule,
  debtStrategy,           wrapperPreference,     withdrawalSequence,
  rebalancingRule,        targetDate,            confidenceTarget,
  horizonAge,             spendingFloor,         goalPriorities
)
```

**Π (the policy space)** is the set of *feasible, meaningful* deviations from π₀. It is enumerated,
not searched exhaustively — an unconstrained search over twelve continuous dimensions would produce
thousands of indistinguishable options and no insight.

### Generation strategy

| Source | Method | Example |
|---|---|---|
| **Driver-led** | Take the top drivers from Attribution; construct options that act on them | Savings rate is the top negative driver → options that raise it |
| **SLO-led** | Any breached SLO generates its runbook's options | Allocation drift → rebalance options, evaluated after tax |
| **Sensitivity-led** | Use cached `∂t_FI/∂x` to find high-leverage, low-effort levers | Wrapper change has high `∂t_FI` and near-zero effort |
| **Structural** | Known high-value Brazilian decisions, evaluated periodically | PGBL headroom, withdrawal sequencing, exemption staging |
| **Goal-led** | Options that resolve a specific goal shortfall | Defer goal, reduce amount, increase funding |
| **User-posed** | The user asks "what if I…?" | Free-form counterfactual |

Options are then **filtered** for feasibility (does the user actually have this lever?), materiality
(does it move `t_FI` beyond Monte Carlo noise?), and distinctness (two options with the same effect
collapse to one).

---

## 2. Evaluation

Every candidate option is evaluated as a **counterfactual on the baseline snapshot** (INV-150),
across the scenario set, using common random numbers:

```
OptionEvaluation
├─ fiDateDelta          {p10, p50, p90}          — distribution, never a point
├─ pFIDelta             percentage points
├─ ruinProbabilityDelta ← often the most important number, and the least asked for
├─ afterTaxCost         Money — routed through ITaxJurisdiction (BR-603)
├─ liquidityImpact      effect on emergency coverage
├─ tailBehaviour        effect under JobLossInCrash and Stagflation specifically
├─ reversibility        ∈ { Free, Costly, Irreversible }
├─ effortRequired       ∈ { OneClick, OneHour, OneDay, Ongoing }
├─ timeToEffect         when the benefit begins
└─ significance         is the delta distinguishable from Monte Carlo noise?
```

### Two evaluation properties that distinguish this from a calculator

**Tail behaviour is evaluated, not just the median.** An option that improves the P50 FI date while
worsening the `JobLossInCrash` outcome is a fundamentally different proposition from one that
improves both — and presenting only the median would hide precisely the risk the user most needs to
see. Every option reports its tail effect.

**Reversibility and effort are first-class.** A strictly better option that is irreversible and
takes a day of work may well be the *wrong* recommendation for someone who would not actually do it.
Ranking on financial impact alone is a common and naive failure.

---

## 3. Ranking

Options are ranked by a **Pareto-first** procedure, not a weighted score:

```
1. Identify the Pareto front over (fiDateDelta, ruinProbabilityDelta, afterTaxCost, effort)
2. Options dominated on every dimension are marked dominatedBy and demoted
3. Within the front, rank by the user's declared preference weights
4. Present 2–5 (BR-601)
```

**Why Pareto first.** Collapsing multiple dimensions into a single score requires choosing weights,
and those weights encode a value judgement that belongs to the user, not the system. Identifying the
non-dominated set is objective; ordering *within* it is preference. Separating the two is what
distinguishes decision support from disguised prescription.

Dominated options are still shown when they are what the user might have expected, explicitly
labelled *"dominated by option 2"* — because knowing why an obvious idea is worse is often more
useful than the recommendation itself.

---

## 4. Mandatory disclosure

Every option carries a **non-empty** `notModelled` list (INV-151, BR-602). This is not boilerplate;
it is generated from the actual gaps in the twin and the model.

```
Option 2 — Redirect R$ 1.200/month from taxable to PGBL

  Effect on FI date        −7 months (P50)  ·  band: −11 to −3 months
  Effect on ruin (tail)    −0.8pp
  After-tax cost           R$ 0 upfront; deferred tax at withdrawal
  Reversibility            Costly — portability has fees; election is effectively permanent
  Effort                   One hour
  Confidence               Verified at 1y horizon; extrapolated beyond 5y

  Assumptions
    • Marginal rate remains in the top bracket through the contribution period
    • Regressive taxation election, 10+ year holding
    • Deduction limit as of ruleset 2026.1.0

  Sensitivities
    • If your marginal rate falls two brackets before withdrawal, the benefit drops ~60%
    • If you withdraw before 8 years, the benefit is negative

  Not modelled
    • Your employer's plan-specific fees
    • Possible future changes to previdência taxation
    • Estate and succession consequences
    • Your subjective preference for liquidity
```

The `Not modelled` section is what makes this decision support rather than advice. A system that
lists what it does not know is asking to be checked; one that does not is asking to be obeyed.

---

## 5. Suppression

Per Seam C (BR-606), Advisory has a mandatory humility mechanism:

| Condition | Behaviour |
|---|---|
| `ForecastReliability = Degraded` for the relevant band | **Suppress** options depending on it; state why |
| `ForecastReliability = Unknown` | Present with an explicit low-confidence caveat |
| Horizon `Unverified` | Present with the extrapolation stated plainly |
| Twin `Degraded` (coverage/freshness) | Present with the data-quality caveat; suppress if coverage < 80% |
| Option delta within Monte Carlo noise | Present as **"no detectable difference"**, never a precise small number |
| Tax ambiguity affects the option | Present the conservative branch; flag the ambiguity |

Explicit suppression message:

> *Atlas is not presenting allocation options right now. Forecast calibration at the 1–5 year
> horizon has drifted outside tolerance since March, and these options depend on that band.
> Model review is open (ADR pending). Data-quality and cashflow options are unaffected.*

**This is a feature, not a failure.** A system that can say "I am not currently reliable enough to
advise you on this" is more trustworthy than one that never can.

---

## 6. Prohibited output

| Prohibited | Rule |
|---|---|
| Naming a specific security | NG-04, BR-605. Options operate at asset-class or wrapper level |
| Imperative phrasing ("you should", "buy", "sell now") | BR-604, enforced by a copy lint in CI |
| A single option presented alone | BR-601. One "option" is a prescription in disguise |
| Options with an empty `notModelled` list | INV-151, runtime-enforced |
| Pre-tax option economics | BR-603, build-failing |
| Urgency framing ("act before…") | Law 10 |
| Options based on market timing or forecasts of direction | NG-10 |

### The copy lint
A CI check scans generated and templated option text for banned imperative constructions. It is a
crude mechanism, and deliberately so: the boundary between "evaluated option" and "instruction" is
mostly *linguistic*, and language drifts under pressure to be helpful. A mechanical check that fails
the build is more durable than a review guideline.

> ⚠️ **The lint runs per locale** (BR-B04). Atlas ships a bilingual surface, and an English-only
> banned list would hold the advice boundary in English while leaving it entirely unenforced in
> Portuguese — passing review, because the lint would still go green. Portuguese carries its own
> traps: **`Ranked Option` translates to `Opção Avaliada`, never `Recomendação`**, which has a
> regulatory connotation the English "option" does not. Adding a locale requires adding its banned
> lists first. See [Localisation Strategy §5](../01-product/11-localisation-strategy.md).

---

## 7. Closing the loop

```
OptionAccepted ──▶ PolicyDeclared (new Policy version citing OptionId, INV-092)
                          │
                          ▼
              behaviour observed over following months
                          │
                          ▼
              PolicyDeviationObserved ──▶ ContributionAdherence SLI
                          │
                          ▼
              Did the accepted option actually happen?
```

This is R33 in the context map, and it matters more than it looks. **An accepted recommendation that
never changes behaviour is worse than no recommendation** — it creates a false belief that the plan
improved. Atlas tracks whether accepted options were actually enacted, and adherence is an SLI.

Dismissed options are recorded with a reason where offered (BR-609) and feed option-generation
preferences — the system learns *what kind* of options this user finds useful, without learning to
push harder.

---

## 8. What this engine deliberately cannot do

| Cannot | Why |
|---|---|
| Execute anything | NG-03. Atlas never moves money |
| Recommend a security | NG-04, regulated activity |
| Predict market direction | NG-10. No grounded model exists |
| Optimise for engagement | NG-11. It has no engagement signal to optimise |
| Tell the user what to do | Law 14. It evaluates; the user decides |
| Hide its uncertainty | Law 8 |

---

**See also:** [Simulation Engine](03-simulation-engine.md) · [Calibration & Scoring](06-calibration-and-scoring.md) · [ADR-0022](../03-architecture/adr/ADR-0022-advice-posture.md) · [Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md)
