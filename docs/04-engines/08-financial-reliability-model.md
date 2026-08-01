# Financial Reliability Model

**Status:** Ratified · **Owner:** SRE / Product · **Context:** C13 (Supporting)

> Google SRE semantics applied to a financial life. Not as decoration — as the mechanism that makes
> "what deserves my attention?" answerable with discipline instead of vibes.

---

## 1. Why SRE maps well here — and where it does not

The mapping works because both domains face the same core problem: **a continuously operating system
with noisy indicators, where alerting on everything is worse than alerting on nothing.**

| SRE concept | Financial analogue | Fidelity |
|---|---|---|
| SLI | Measurable indicator of financial health | ✅ Direct |
| SLO | Target the indicator must hold | ✅ Direct |
| Error budget | Permitted deviation before action | ✅ Direct — arguably *better*, because financial budgets have natural units |
| Burn rate | Speed of budget consumption | ✅ Direct |
| Incident | Sustained breach requiring response | ✅ Direct |
| Runbook | Documented response procedure | ✅ Direct |
| Postmortem | Blameless analysis after resolution | ⚠️ Needs care — see §7 |
| Toil | Recurring manual work | ✅ Direct |
| Availability | — | ❌ **No analogue.** Do not force one |

**Where the metaphor breaks and must not be forced:** there is no "uptime" for a financial life, and
inventing one would produce exactly the kind of meaningless composite number this product exists to
avoid. Every SLI below measures something real, in its own unit.

---

## 2. The SLI catalogue

Every SLI must be **noise-immune**: an indicator that market marks alone can move is rejected at
definition time (BR-500, INV-141). This single rule eliminates most of what finance apps call
"health".

### Liquidity & resilience

| SLI | Definition | SLO | Error budget unit |
|---|---|---|---|
| `EmergencyCoverage` | Liquid assets ÷ monthly essential spend | ≥ 6 months | Month-months below target |
| `LiquidityRatio` | Assets convertible in ≤ 30 days ÷ 12-month committed outflows | ≥ 1.0 | Ratio-months |
| `IncomeConcentration` | Largest income source ÷ total income | ≤ 80% | Percentage-point-months |

### Savings & capital formation

| SLI | Definition | SLO | Error budget unit |
|---|---|---|---|
| `SavingsRate` | (Income − spending) ÷ income, trailing 3 months, real | ≥ 30% | **R$ of cumulative shortfall** |
| `ContributionAdherence` | Actual contributions ÷ policy-declared contributions | ≥ 95% | R$ shortfall |
| `DiscretionaryBudget` | Discretionary spend vs planned | ≤ plan | **R$ over plan — the headline error budget** |
| `SavingsVelocity` | 12-month change in real savings capacity | ≥ 0 | — |

### Structure & policy adherence

| SLI | Definition | SLO | Error budget unit |
|---|---|---|---|
| `AllocationDrift` | Max deviation of any asset class from its IPS band | within ±5pp | pp-months outside band |
| `RebalanceLatency` | Days a band breach has gone unaddressed | ≤ 45 days | Day-breaches |
| `TaxEfficiency` | Realised tax drag vs modelled optimal-sequence baseline | ≤ +0.5pp | pp of drag |
| `WrapperUtilisation` | Deductible contribution headroom used (e.g. PGBL) | ≥ 90% | R$ of unused headroom |
| `DebtCostSpread` | Weighted debt cost − expected real portfolio return | ≤ 0 | pp-months |

### Data integrity — the foundation of everything above

| SLI | Definition | SLO | Error budget unit |
|---|---|---|---|
| `Coverage` | % of net worth in reconciled sources | ≥ 95% | pp-days |
| `Freshness` | Hours since last successful reconciliation per source | ≤ 72 h | Hour-breaches |
| `Categorisation` | % of expense value with confirmed classification | ≥ 90% | pp-days |
| `ReconciliationDrift` | Source-reported minus ledger-derived balance | ≤ R$ 1,00 | R$ |

### System self-assessment

| SLI | Definition | SLO |
|---|---|---|
| `ForecastReliability` | Calibration status per horizon band | `Verified` for 1m–1y |
| `ForecastFreshness` | Age of newest artifact vs newest material state change | ≤ 24 h |

**On `SavingsRate` denominated in R$ of shortfall.** This is where the financial error budget is
*better* than the SRE original. "You are 4 percentage points below target" is abstract. "You have
consumed R$ 8.400 of your R$ 12.000 annual savings error budget, at a burn rate that exhausts it in
March" is concrete, has a natural unit, and states a deadline. The metaphor earns its place here.

---

## 3. The Discretionary Error Budget

The most important application, and the honest replacement for budgeting (NG-01):

```
Annual FI plan tolerates R$ 18.000 of discretionary overspend
  before the P50 FI date moves by more than 90 days.

Budget:    R$ 18.000 / year
Consumed:  R$  7.200  (40%)  ·  elapsed: 33% of year
Burn rate: 1.21× — exhausts in ~month 10
Cost so far: 12 FI-days
```

This reframes spending from **moralising** ("you spent too much on restaurants") to
**engineering** ("you are burning your discretionary budget 21% fast; here is the FI-day cost").
Same discipline, correct unit, no guilt — and it is derived from the forecast rather than asserted.

The budget size is *computed*, not chosen: it is the overspend that moves the P50 FI date by the
user's declared tolerance.

---

## 4. Burn-rate alerting

Multi-window, following SRE practice (BR-507):

| Window pair | Burn rate | Meaning | Severity |
|---|---|---|---|
| 1 month + 3 months | > 3× | Rapid, sustained overshoot | SEV-2 |
| 3 months + 12 months | > 2× | Persistent drift | SEV-3 |
| 12 months | > 1× | Will exhaust within the period | SEV-4 |

Two windows prevent both failure modes: a single short window flaps on one unusual month; a single
long window notices a slow bleed far too late.

---

## 5. Incidents

```
SLO breached ──2 consecutive evaluations──▶ INCIDENT DECLARED (BR-502)
                                                    │
                                          ┌─────────▼─────────┐
                                          │ Runbook attached  │
                                          │ Severity assigned │
                                          │ Timeline started  │
                                          └─────────┬─────────┘
                                    Acknowledged → Mitigating → Resolved
                                                    │
                                          Postmortem within 7 days (BR-503)
```

| Severity | Criterion | Example |
|---|---|---|
| **SEV-1** | Threatens financial viability | Income loss with < 3 months coverage |
| **SEV-2** | Materially changes the FI trajectory | Savings rate below target 3 months running |
| **SEV-3** | Persistent drift | Allocation outside band > 45 days |
| **SEV-4** | Hygiene | Categorisation coverage below target |

### Runbook catalogue

| ID | Trigger | Outline |
|---|---|---|
| `RB-FIN-01` | Emergency coverage below target | Pause discretionary contributions, rebuild floor, re-forecast |
| `RB-FIN-02` | Income loss | Activate JobLoss scenario as base, evaluate runway, review committed outflows |
| `RB-FIN-03` | Savings rate breach | Attribute the shortfall, review discretionary burn, evaluate ranked options |
| `RB-FIN-04` | Allocation drift | Evaluate rebalance options **after tax** — tax cost may exceed drift cost |
| `RB-FIN-05` | Debt cost exceeds expected return | Compare amortisation vs investment, after tax, with both reversibility profiles |
| `RB-FIN-06` | Data coverage degraded | Identify unreconciled sources, re-import, reconcile |
| `RB-FIN-07` | Tax exemption headroom expiring | Evaluate year-end sequencing options |
| `RB-FIN-08` | Forecast reliability degraded | Suppress affected advice, open model review |

**Every runbook ends by re-running the forecast and recording the outcome.** That is what closes the
loop between response and effect — and what makes the postmortem's "did it work?" answerable.

---

## 6. The Health Score

A composite 0–100 across six dimensions, **each independently noise-immune** (BR-505):

| Dimension | Weight | Component SLIs |
|---|---|---|
| Resilience | 25% | Emergency coverage, liquidity, income concentration |
| Capital formation | 25% | Savings rate, contribution adherence, savings velocity |
| Structure | 20% | Allocation drift, rebalance latency, debt cost spread |
| Tax efficiency | 15% | Tax efficiency, wrapper utilisation |
| Data integrity | 10% | Coverage, freshness, categorisation, reconciliation |
| Forecast reliability | 5% | Calibration status |

**Constraints:**
- The score is **never shown without its breakdown** available in one interaction (BR-506). An
  unexplained composite is exactly the kind of number Law 1 exists to ban.
- No component may move on market marks alone. Portfolio value appears **nowhere** in this score.
- A dimension at zero caps the total at 60, regardless of the others — you cannot compensate for no
  emergency fund by having excellent tax efficiency.

---

## 7. Blameless postmortems, for one person

Blamelessness normally means "don't blame a colleague". Here it means something subtler and harder:
**don't blame yourself; blame the system.**

The template forbids attributing cause to the person (BR-504) and requires a systemic cause:

```
Incident: SavingsRate SLO breach, 3 months, SEV-2

❌ "I overspent because I was undisciplined."
✅ "Discretionary spend exceeded plan for 3 months. No burn-rate signal existed at the
    1-month window, so the trend was invisible until breach. Contributing factor: two
    unplanned annual expenses (insurance, IPVA) were not in the recurring model."

Corrective actions:
  1. Add a 1-month burn-rate window for the discretionary budget.
  2. Model annual lumpy expenses as a seasonal component rather than episodic.
  3. Add a 60-day lookahead for known annual outflows.
```

Each corrective action is a **system change**, not a resolution to try harder. That is the entire
value of importing this practice: resolutions decay in weeks; system changes persist.

---

## 8. What this model deliberately refuses

| Refused | Why |
|---|---|
| An "availability" metric for financial life | No meaningful analogue; would be a made-up number |
| Portfolio value in any SLI | Not controllable; moves on noise |
| Return-based SLOs | Would alert on stochastic drivers, violating ADR-0009 |
| Peer-relative SLOs | NG-05; the only valid benchmark is the user's own prior trajectory |
| A single unexplained health number | Law 1 |
| Streak-style pressure to resolve incidents | Law 10; incidents are informational, not punitive |

---

**See also:** [Attribution Engine](04-attribution-engine.md) · [Gamification Strategy](../01-product/06-gamification-strategy.md) · [Observability Strategy](../03-architecture/07-observability-strategy.md)
