# Epics & Backlog

**Status:** Living · **Owner:** Product

Epics map to bounded contexts and milestones. Sizing is **relative complexity**, not time —
single-maintainer velocity is too variable for time estimates to be honest.

Size scale: **XS** trivial · **S** a sitting · **M** a weekend · **L** several weekends · **XL** a
milestone in itself · **XXL** should be split.

---

## Epic tree

```
E1  Kernel & Ledger Foundation                    M0    XL
E2  Ingestion & Reconciliation                    M1    XL
E3  Positions, Valuation & Market Data            M1–M2 L
E4  Liabilities & Cashflow Modelling              M2–M3 L
E5  Goals, Policy & Intent                        M2–M3 M
E6  Brazilian Tax Engine                          M3    XXL → split
E7  Digital Twin                                  M3    L
E8  Forecast & Simulation Engine                  M3–M4 XXL → split
E9  Attribution & Signal Gate                     M4    XL
E10 Financial Reliability Model                   M4    L
E11 Mission Control & Surfaces                    M4    XL
E12 Advisory & Decision Support                   M5    XL
E13 Calibration & Scoring                         M4/M6 L
E14 Progression                                   M6    M
E15 Narrative & AI                                M6    M
E16 Platform, DevOps & Observability               all   L
E17 Data Portability & DR                         M2    M
E18 Localisation (pt-BR / en)                     M4–M6 M
```

---

## E1 — Kernel & Ledger Foundation · M0 · XL

| Story | Size | FRs |
|---|---|---|
| `Money` value object: integer minor units, commodity-safe arithmetic | S | — |
| Division with remainder allocation (largest-remainder) | S | — |
| `Commodity` taxonomy including `Unmodelled` | XS | — |
| Bitemporal interval types and operations | M | FR-104 |
| Chart of accounts with type immutability after first posting | S | FR-102 |
| Journal entry with per-commodity balance enforcement | M | FR-103 |
| Multi-commodity entries via trading accounts | M | FR-103 |
| Bitemporal persistence schema and indexes | L | FR-104 |
| As-of balance queries requiring both time coordinates | M | FR-105 |
| Correction by reversal + replacement | M | FR-106 |
| Event sourcing + transactional outbox | L | — |
| Module skeleton, `IAtlasModule`, composition root | M | — |
| Architecture tests MR-1..MR-10 | M | — |
| Roslyn analysers: banned types, ambient clock | M | — |
| Golden replay test | S | — |

## E2 — Ingestion & Reconciliation · M1 · XL

| Story | Size | FRs |
|---|---|---|
| `ISourceAdapter` contract + raw payload archive | M | FR-112 |
| Idempotency key computation from raw records | S | FR-109 |
| CSV adapter with user-defined column mapping | L | FR-107 |
| OFX adapter | M | FR-108 |
| Manual entry as a first-class source | S | FR-101 |
| Fuzzy cross-source duplicate detection + resolution queue | L | FR-110 |
| Entry proposal + confirmation flow, auto-confirm rules | M | — |
| Source reconciliation with tolerance and drift detection | M | FR-111 |
| Golden-file corpus per adapter | M | — |
| Expense classification with versioning and audit trail | M | FR-301 |

## E3 — Positions, Valuation & Market Data · M1–M2 · L

| Story | Size | FRs |
|---|---|---|
| Lot tracking with instrument classification | M | FR-201 |
| *Custo médio* basis computation | M | FR-202 |
| Corporate actions: split, bonus, rights, merger, spin-off | L | FR-203 |
| Distributions: dividend, JCP, rendimento, amortisation | M | FR-204 |
| B3 daily close mark ingestion | M | FR-205 |
| BCB SGS series ingestion (IPCA, SELIC, CDI, IGP-M) | M | FR-206 |
| Valuation with per-mark staleness recording | M | FR-207–208 |
| Position ↔ ledger reconciliation invariant | S | — |

## E4 — Liabilities & Cashflow · M2–M3 · L

| Story | Size | FRs |
|---|---|---|
| Loan aggregate: SAC and Price schedules | L | FR-307 |
| Indexation (TR, IPCA, IGP-M) and rate resets | M | FR-308 |
| Extra amortisation with explicit mode | M | FR-309 |
| Instalment ↔ ledger reconciliation | S | — |
| Income streams incl. 13th, bonus, FGTS | M | FR-306 |
| Recurring pattern detection | M | FR-302 |
| Per-category spending model fitting | L | FR-303 |
| Personal inflation index and IPCA divergence | M | FR-304–305 |
| Human capital valuation with `marketBeta` | L | FR-310 |
| Employment hazard, severance, search duration | M | FR-311 |

## E5 — Goals, Policy & Intent · M2–M3 · M

| Story | Size | FRs |
|---|---|---|
| Goals with total-order priority and flexibility | M | FR-401 |
| Investment Policy Statement with allocation bands | M | FR-402 |
| Policy aggregate, immutable and versioned | M | FR-403, 406 |
| Target date, confidence target, horizon age | S | FR-405 |
| Withdrawal sequence declaration | M | FR-404 |
| Life events, planned and occurred | S | FR-407 |

## E6 — Brazilian Tax Engine · M3 · **XXL → split into E6a–E6d**

### E6a — Framework (L)
`ITaxJurisdiction` interface · ruleset schema and loader · effective-date resolution ·
`RuleTrace` infrastructure · ruleset validation (continuity, legal basis) · purity enforcement.

### E6b — Core regimes (XL)
`RendaVariavel` with monthly exemption · `RendaFixa` with tabela regressiva · `FII` distribution
and gain treatment · `Isento` instruments · basis interaction with corporate actions.

### E6c — Funds & pensions (L)
`FundoAberto` with come-cotas as discrete scheduled events · equity fund treatment ·
`FundoFechado` periodic regime · PGBL deductibility · VGBL gains-only · regime election modelling.

### E6d — Optimisation & projection (L)
Tax drag projection over horizon · withdrawal sequence evaluation · loss carry-forward and
offsetting · exemption headroom tracking · ambiguity detection with conservative resolution ·
performance work for the simulation hot path.

## E7 — Digital Twin · M3 · L

| Story | Size | FRs |
|---|---|---|
| Snapshot assembly at a fixed consistency point | L | FR-601 |
| Canonical serialisation + cross-platform hash test | M | — |
| Provenance: coverage, freshness, degradations | M | FR-602 |
| Content-address deduplication | S | FR-603 |
| Materiality gate using cached sensitivities | M | FR-604 |
| Schema versioning + historical corpus test | M | — |

## E8 — Forecast & Simulation · M3–M4 · **XXL → split into E8a–E8d**

### E8a — Engine core (XL)
Path evolution loop · counter-based RNG for deterministic parallelism · convergence checking and
run rejection · artifact assembly and storage · determinism CI gate.

### E8b — Return models (L)
Stationary block bootstrap (joint across assets) · regime-switching model · ensemble aggregation ·
model uncertainty · i.i.d. baseline for comparison.

### E8c — Life dynamics (L)
Non-stationary spending with the retirement smile · correlated employment shocks · liability
servicing · policy application · tax integration per period · FI and ruin condition tests.

### E8d — Scenarios & counterfactuals (L)
Scenario algebra and composition with correlation · standard scenario library · counterfactual
evaluation on a fixed baseline snapshot · common random numbers and variance reduction.

## E9 — Attribution & Signal Gate · M4 · XL

| Story | Size | FRs |
|---|---|---|
| Analytic first-order attribution from cached sensitivities | M | FR-701 |
| Monte Carlo Shapley with escalation | L | FR-701 |
| Driver taxonomy and classification | M | FR-702 |
| Residual discipline with rejection | S | FR-703 |
| Model-version guard | S | FR-704 |
| `ModelChange` and `Restatement` channels | M | FR-705–706 |
| **Signal Gate: class, materiality, significance, dedup** | L | FR-707 |
| Suppression storage and variance inspector | M | FR-708–709 |
| Controllable Drift and driver ranking | M | FR-710–711 |

## E10 — Financial Reliability · M4 · L

SLI catalogue and evaluation · SLO definitions with noise-immunity validation · error budgets in
natural units · Discretionary Error Budget derived from FI-day tolerance · multi-window burn-rate
alerting · incident lifecycle · runbook catalogue `RB-FIN-01..08` · postmortem template with
blamelessness enforcement · Health Score with dimension breakdown and the zero-cap rule.

## E11 — Mission Control & Surfaces · M4 · XL

Card framework with build-time question validation · six-card Mission Control · Change Feed with
suppressed-movement summary · Operations surface (SLOs, incidents, runbooks) · universal drill-down
spine · provenance viewer · mobile quick capture with offline queue · design system implementation.

## E12 — Advisory · M5 · XL

Policy space enumeration from six sources · option filtering (feasibility, materiality,
distinctness) · counterfactual evaluation across scenarios · Pareto front computation · preference
ordering · disclosure generation from real model gaps · suppression on degraded reliability ·
copy lint · option → policy loop closure · dismissal reasons.

## E13 — Calibration · M4 registration / M6 scoring · L

Forecast claim registration at emission · outcome observation and resolution · Brier with
decomposition · CRPS · PIT histograms · reliability diagrams · per-band reporting with `Unknown` ·
reliability status and the advice gate · CUSUM drift detection → model review · back-test harness ·
synthetic calibration verification.

## E14 — Progression · M6 · M

Eligible event allow-list · Discipline Index · streaks with suspend semantics · capped XP ·
Architecture Levels 1–8 · velocity metrics (informational) · perverse-incentive audit for every
mechanic · `no-frills build` verification.

## E15 — Narrative & AI · M6 · M

`FactSet` construction · narrative templates · LLM integration behind a provider port ·
**traceability verification at generation time** · rejection and structured fallback · golden
corpus regression · prompt minimisation · generated-content labelling.

## E16 — Platform, DevOps & Observability · all milestones · L

Bicep modules · CI pipeline with all twelve gates · determinism gate · historical corpus gate ·
rule-coverage gate · `no-frills build` gate · cost-delta gate · OTel instrumentation · dashboards ·
alert definitions with runbooks · Renovate · secret scanning · SBOM · drift detection.

## E18 — Localisation · M4–M6 · M

| Story | Size | FRs |
|---|---|---|
| Locale in request context; locale-keyed `FactSet.displayString` | M | FR-941 |
| Server-side formatters: number, currency, percent, date, duration, magnitude | M | FR-941 |
| ICU message catalogue + completeness gate | M | FR-940 |
| Flutter runtime locale switching, pt-BR default | S | FR-940 |
| Terminology lint: Tier-1 untranslated, Tier-2 canonical pairing | M | FR-942 |
| **Per-locale copy lint with banned lists** | M | FR-943 |
| No-concatenation lint for user-facing text paths | S | — |
| Rule-trace label localisation, Portuguese citations preserved | S | FR-945 |
| Per-locale narrative generation + second golden corpus | M | FR-944 |
| `lang` attribution + layout snapshots at max Dynamic Type, both locales | S | NFR-812–813 |

**Sequencing note.** The per-locale copy lint (FR-943) must land **with or before** the first
Portuguese option copy in M5 — not after. Shipping Portuguese advice text against an English-only
banned list would leave the ADR-0022 boundary unenforced in the default language, and the build would
still be green.

## E17 — Data Portability & DR · M2 · M

Full export command · `SCHEMA.md` generation · Parquet/JSON/CSV writers · re-import into clean
database · **CI export→restore→compare job** · weekly scheduled export to user-controlled storage ·
restore drill runbook · cryptographic erasure implementation.

---

## Backlog hygiene

| Rule | Detail |
|---|---|
| Every story cites its FR | A story with no requirement is scope creep |
| **XXL is not a valid size** | It means the epic is not understood yet; split before starting |
| No story without acceptance criteria | See [User Stories](10-user-stories.md) |
| No story that violates a Product Law | Reviewer may block citing the law alone |
| Technical debt is a first-class item | With an interest rate and a paydown date |
| Nothing enters M(n+1) until M(n)'s exit gate passes | Gates are not negotiable |

---

**See also:** [Roadmap & Milestones](08-roadmap-and-milestones.md) · [User Stories](10-user-stories.md) · [Functional Requirements](02-functional-requirements.md)
