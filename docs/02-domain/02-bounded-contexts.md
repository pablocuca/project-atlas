# Bounded Contexts

**Status:** Ratified · **Owner:** Domain / CTO · **Last reviewed:** 2026-08-01

Seventeen contexts. Each has one owner module, one language, and one reason to change.
**A context boundary is a compile-time boundary** — see [Modular Monolith](../03-architecture/03-modular-monolith.md).

---

## Classification

Strategic classification determines where effort goes. **Core** contexts get the best design, the
deepest tests, and the most senior attention. **Generic** contexts get bought, borrowed, or written
once and left alone.

| Class | Contexts | Investment posture |
|---|---|---|
| 🔴 **Core** | Taxation-BR, Forecast, Attribution, Calibration, Advisory, Digital Twin | Maximum depth. Property-based and golden-file tested. Never outsourced |
| 🟡 **Supporting** | Ledger, Position & Valuation, Cashflow & Behaviour, Liabilities, Human Capital, Goals & Policy, Reliability, Progression, Narrative | Solid, conventional, well-tested. Correctness over cleverness |
| ⚪ **Generic** | Ingestion, Market Data, Identity, Notification | Minimum viable. Prefer boring, replaceable implementations |

> **Why Ledger is Supporting, not Core.** Double-entry bookkeeping is a 500-year-old solved
> problem. Getting it *right* is mandatory; inventing anything there is a mistake. The
> differentiation is in what Atlas does *above* the ledger.

---

## 🔴 Core contexts

### C1 — Taxation (Brazil)
**Answers:** what does the government take, when, and how does sequencing change it?
**Owns:** `TaxLot`, `TaxRegime`, `TaxEvent`, `TaxYear`, `WithholdingRecord`, `ExemptionClaim`, `DARFObligation`
**Language:** come-cotas, tabela regressiva, isenção, IRRF, ganho de capital, custo médio, DARF, PGBL/VGBL
**Why core:** tax drag and withdrawal sequencing dominate `t_FI` in Brazil. Every other engine
routes through it. This is the deepest moat in the product.
**Boundary discipline:** exposes a *pure function* interface — `(lots, event, date) → tax
consequence`. Holds no user state, performs no I/O, is fully deterministic and replayable.

### C2 — Forecast
**Answers:** what is the distribution of outcomes from here?
**Owns:** `ForecastArtifact`, `ModelVersion`, `AssumptionSet`, `Path`, `ReturnModel`, `Seed`
**Language:** block bootstrap, regime, sequence risk, ruin, percentile, ensemble
**Why core:** the North Star is produced here. Its integrity is the product's integrity.

### C3 — Attribution
**Answers:** what caused this change, and does it deserve the user's attention?
**Owns:** `Delta`, `Driver`, `DriverClass`, `Attribution`, `SignalGate`, `ChangeEvent`, `VarianceBudget`
**Language:** controllable, structural, stochastic, Shapley share, significance, materiality
**Why core:** this is the product's actual differentiator. Forecasting is commodity; **honest
attribution and disciplined silence are not**.

### C4 — Calibration
**Answers:** has this system historically been right when it said 85%?
**Owns:** `CalibrationRecord`, `ForecastScore`, `ReliabilityCurve`, `DriftDetection`
**Language:** Brier score, CRPS, PIT, reliability diagram, sharpness, drift
**Why core:** it is the only mechanism by which probabilistic claims become trustworthy, and no
competitor ships it.

### C5 — Advisory
**Answers:** what are my best available options, and what does each cost?
**Owns:** `PolicySpace`, `Option`, `OptionEvaluation`, `Ranking`, `TradeoffDisclosure`, `Suppression`
**Language:** policy, counterfactual, dominance, Pareto front, opportunity cost, unknown
**Why core:** it converts analysis into decisions. It is also where the legal boundary lives.

### C6 — Digital Twin
**Answers:** what is the complete, consistent state of this financial life right now?
**Owns:** `TwinState`, `TwinSnapshot`, `MaterialityGate`, `SnapshotHash`
**Language:** snapshot, state version, materiality, consistency point
**Why core:** it is the integration surface for every other context and the input contract of every
forecast. A weak twin makes everything above it unreliable.

---

## 🟡 Supporting contexts

### C7 — Ledger
**Answers:** what happened, when was it true, and when did we learn it?
**Owns:** `Account`, `JournalEntry`, `Posting`, `Commodity`, `Money`, `Correction`
**Invariants:** postings sum to zero per entry per commodity; append-only; bitemporal.
**Downstream of:** nothing. **Upstream of:** everything.

### C8 — Position & Valuation
**Answers:** what do I hold, at what basis, worth what?
**Owns:** `Lot`, `Position`, `CorporateAction`, `Valuation`, `MarkSet`
**Note:** owns *cost basis mechanics*; the Tax context owns *tax treatment of that basis*.

### C9 — Cashflow & Behaviour
**Answers:** what do I earn and spend, how predictably, and what is my real inflation?
**Owns:** `IncomeStream`, `ExpenseRecord`, `Category`, `RecurringPattern`, `SpendingModel`,
`PersonalInflationIndex`, `SpendingFloor`
**Note:** the `SpendingModel` is a *statistical* object — a fitted, non-stationary process, not a budget.

### C10 — Liabilities
**Answers:** what do I owe, on what terms, and what does paying it early actually buy?
**Owns:** `Loan`, `AmortisationSchedule`, `Instalment`, `RateReset`, `Indexation`
**Language:** SAC, Price/Français, TR, IPCA-indexed, CET, saldo devedor

### C11 — Human Capital
**Answers:** what is my future earning power worth, and how correlated is it with my portfolio?
**Owns:** `CareerTrack`, `EarningsTrajectory`, `EmploymentRisk`, `HumanCapitalValuation`
**Why it exists separately:** for most users this is the single largest asset, and its
**correlation with equity drawdowns** is the dominant tail risk. Modelling it inside Cashflow
would hide that.

### C12 — Goals & Policy
**Answers:** what am I actually trying to achieve, and what have I committed to doing?
**Owns:** `Goal`, `GoalPriority`, `LifeEvent`, `Policy`, `InvestmentPolicyStatement`,
`RiskTolerance`, `WithdrawalSequence`, `ConfidenceTarget`
**Note:** `Policy` here is the domain object that Advisory perturbs to form the Policy Space.

### C13 — Financial Reliability
**Answers:** which parts of my financial operation are outside their envelope?
**Owns:** `FinancialSLI`, `FinancialSLO`, `ErrorBudget`, `BurnRate`, `Incident`, `Runbook`,
`Postmortem`, `HealthScore`
**Note:** deliberately mirrors Google SRE semantics, including blameless postmortems.

### C14 — Progression
**Answers:** am I behaving consistently with what I said I would do?
**Owns:** `ProgressionProfile`, `Streak`, `DisciplineIndex`, `OperationalXP`, `ArchitectureLevel`
**Hard constraint:** may read only *process* facts. **Structurally forbidden** from subscribing to
valuation or return events — enforced by module dependency rules, not convention.

### C15 — Narrative
**Answers:** how do I say this in plain language without lying?
**Owns:** `FactSet`, `NarrativeTemplate`, `Rendering`, `TraceabilityProof`
**Hard constraint:** consumes only validated `FactSet` objects. Has **no dependency on Ledger,
Forecast, or Tax**. Cannot compute. See [AI Strategy](../06-governance/02-ai-strategy.md).

---

## ⚪ Generic contexts

### C16 — Ingestion
**Owns:** `Source`, `ImportBatch`, `ParsedRow`, `EntryProposal`, `IdempotencyKey`, `Reconciliation`
**Posture:** every external system gets an **anti-corruption layer**. No external schema ever
reaches the Ledger. Manual entry is a first-class Source, not a fallback.

### C17 — Market Data
**Owns:** `Quote`, `MarkObservation`, `IndexSeries` (IPCA, SELIC, CDI, IBOV, IFIX), `FXRate`, `Staleness`
**Posture:** cache-first, tolerant of gaps, explicit about staleness. Never blocks a forecast —
a stale mark is used *with its staleness recorded*.

### C18 — Identity & Access *(seam only)*
**Owns:** `Principal`, `TenantId`, `Session`, `Credential`
**Posture:** single principal today. `TenantId` threaded through every aggregate root from the
first commit so multi-tenancy is a migration, not a rewrite. See [ADR-0011](../03-architecture/adr/ADR-0011-single-tenant-core.md).

### C19 — Notification
**Owns:** `Channel`, `NotificationPolicy`, `Dispatch`, `SuppressionRule`, `Briefing`
**Posture:** subscribes to `ChangeEvent` and `IncidentDeclared` only. **Cannot be triggered by raw
deltas** — the Signal Gate is upstream and non-bypassable.

---

## Dependency direction (strict)

```
Identity ─────────────────────────────────────────┐
                                                  │
Ingestion ──▶ Ledger ──▶ Position & Valuation ──┐  │
                 │              │               │  │
Market Data ─────┴──────────────┘               │  │
                                                ▼  ▼
Cashflow ──┐                              ┌── Taxation (pure)
Liabilities├──────────────────────────────┤
HumanCap.  │                              │
Goals&Policy┘                             │
     │                                    │
     └──────────▶ DIGITAL TWIN ◀──────────┘
                       │
                       ▼
                   Forecast ──▶ Attribution ──▶ Reliability ──▶ Notification
                       │             │              │
                       ▼             ▼              ▼
                  Calibration    Advisory      Progression
                                     │
                                     ▼
                                 Narrative
```

**Enforced rules:**
1. No context may depend on a context to its right or below it in this diagram.
2. **Taxation depends on nothing** — it is a pure function library.
3. **Narrative depends only on Advisory/Attribution output contracts**, never on domain internals.
4. **Progression may not reference Valuation or Forecast.**
5. Cross-context communication is by **published domain event or published contract only** — never
   by shared table or direct type reference.

Violations fail the build. See [Modular Monolith §4](../03-architecture/03-modular-monolith.md).
