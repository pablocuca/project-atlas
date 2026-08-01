# Functional Requirements

**Status:** Ratified · **Owner:** Product

`FR-` identifiers are **permanent**. Deprecate; never renumber. Every FR maps to at least one
acceptance test. Priority: **M** must-have · **S** should-have · **C** could-have · **W** won't-have-now.

---

## FR-1xx — Ledger and truth

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-101 | Record a financial transaction manually with date, amount, commodity, accounts, description | M | M0 |
| FR-102 | Maintain a hierarchical chart of accounts typed Asset/Liability/Equity/Income/Expense | M | M0 |
| FR-103 | Enforce per-commodity balance on every journal entry | M | M0 |
| FR-104 | Record both valid time and decision time on every fact | M | M0 |
| FR-105 | Query any balance at an arbitrary (valid time, decision time) pair | M | M0 |
| FR-106 | Correct a prior entry by reversal + replacement, preserving the original | M | M0 |
| FR-107 | Import transactions from CSV with a user-defined column mapping | M | M1 |
| FR-108 | Import OFX statements | S | M1 |
| FR-109 | Detect and reject duplicate imports via idempotency keys | M | M1 |
| FR-110 | Detect probable cross-source duplicates and queue for human resolution | M | M1 |
| FR-111 | Reconcile a source's reported balance against the ledger-derived balance | M | M1 |
| FR-112 | Archive every raw source payload before parsing, permanently | M | M1 |
| FR-113 | Quick-capture an expense on mobile in under 10 seconds | M | M2 |
| FR-114 | Capture an expense while offline and sync on reconnect | S | M2 |
| FR-115 | Parse broker *notas de corretagem* (PDF) | C | M4 |
| FR-116 | Import B3 investor-area position exports | C | M4 |
| FR-117 | Ingest via a commercial Open Finance aggregator | C | M5+ |

## FR-2xx — Positions, valuation, market data

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-201 | Track lots with quantity, unit cost, acquisition date, instrument class | M | M1 |
| FR-202 | Compute cost basis using *custo médio* for Brazilian equities | M | M1 |
| FR-203 | Apply corporate actions preserving total basis (splits, bonuses, mergers, spin-offs) | M | M2 |
| FR-204 | Record distributions distinguishing dividends, JCP, *rendimentos*, and amortisations | M | M2 |
| FR-205 | Ingest daily closing marks for held commodities | M | M2 |
| FR-206 | Ingest IPCA, SELIC, CDI, IGP-M series from BCB | M | M2 |
| FR-207 | Value the portfolio at any instant, recording the staleness of every mark used | M | M2 |
| FR-208 | Flag any valuation using marks beyond the staleness tolerance | M | M2 |
| FR-209 | Exclude `Unmodelled` commodities from forecasts and report them separately | M | M3 |
| FR-210 | Track foreign holdings with FX conversion | C | M5 |

## FR-3xx — Cashflow, liabilities, human capital

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-301 | Classify each expense category as Essential / Committed Discretionary / Discretionary, versioned with an audit trail | M | M1 |
| FR-302 | Detect recurring expense patterns and their cadence | M | M2 |
| FR-303 | Fit a per-category spending model (recurring, seasonal, episodic, trending) | M | M3 |
| FR-304 | Compute a personal inflation index from the user's own basket | M | M3 |
| FR-305 | Report divergence between personal inflation and IPCA | S | M3 |
| FR-306 | Model income streams including 13th salary, bonus, and FGTS | M | M2 |
| FR-307 | Model a loan with SAC or Price amortisation, indexation, and rate resets | M | M2 |
| FR-308 | Regenerate an amortisation schedule after a reset or extra amortisation, retaining prior schedules | M | M2 |
| FR-309 | Distinguish extra amortisation modes (reduce term vs reduce instalment) | M | M2 |
| FR-310 | Value human capital with an explicit market correlation (`marketBeta`) | M | M3 |
| FR-311 | Model employment hazard rate, severance, and expected search duration | M | M3 |

## FR-4xx — Goals, policy, intent

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-401 | Define goals with amount, date, priority (total order), and flexibility | M | M2 |
| FR-402 | Declare an Investment Policy Statement with allocation bands | M | M3 |
| FR-403 | Declare a Policy: savings rate, contribution schedule, debt strategy, wrapper preference | M | M3 |
| FR-404 | Declare a withdrawal sequence for decumulation | M | M4 |
| FR-405 | Set Target Date, Confidence Target, and Horizon Age | M | M3 |
| FR-406 | Version every Policy change with its reason and originating option | M | M3 |
| FR-407 | Record life events, planned and occurred | S | M3 |
| FR-408 | Assess risk tolerance and reflect it in allocation guidance | S | M4 |

## FR-5xx — Taxation

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-501 | Classify every lot into its Brazilian tax regime | M | M3 |
| FR-502 | Compute disposal tax consequences with a full rule trace | M | M3 |
| FR-503 | Apply the fixed-income regressive table by holding period | M | M3 |
| FR-504 | Model come-cotas as discrete scheduled events on open-ended funds | M | M3 |
| FR-505 | Track monthly equity-sale exemption consumption | M | M3 |
| FR-506 | Apply FII distribution exemption conditions | M | M3 |
| FR-507 | Model PGBL deductibility and VGBL gains-only taxation, with regime election | M | M4 |
| FR-508 | Carry forward and offset losses within regime scope | M | M4 |
| FR-509 | Project tax drag over the forecast horizon | M | M3 |
| FR-510 | Evaluate withdrawal sequences after tax | M | M4 |
| FR-511 | Resolve every computation against a versioned, effective-dated ruleset | M | M3 |
| FR-512 | Flag genuinely ambiguous treatments and compute the conservative branch | M | M3 |
| FR-513 | Produce a DARF worksheet for user verification (never a filing) | C | M5 |

## FR-6xx — Twin, forecast, simulation

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-601 | Assemble a complete, content-addressed twin snapshot at a fixed consistency point | M | M3 |
| FR-602 | Record provenance: coverage, freshness, and known degradations | M | M3 |
| FR-603 | Deduplicate identical snapshots by content hash | M | M3 |
| FR-604 | Decide via a materiality gate whether a state change warrants re-forecast | M | M3 |
| FR-605 | Produce an immutable, content-addressed forecast artifact | M | M3 |
| FR-606 | Simulate with block bootstrap and regime-switching models as an ensemble | M | M3 |
| FR-607 | Report FI date as a distribution (P10/P50/P90) | M | M3 |
| FR-608 | Report `P(FI by target date)` with a confidence band | M | M3 |
| FR-609 | Report ruin probability by horizon decade | M | M3 |
| FR-610 | Report model uncertainty as ensemble spread | M | M3 |
| FR-611 | Verify Monte Carlo convergence and reject under-converged runs | M | M3 |
| FR-612 | Reproduce any artifact bit-identically from its key | M | M3 |
| FR-613 | Compute all projections after tax and in real terms | M | M3 |
| FR-614 | Model employment loss correlated with market stress regimes | M | M3 |
| FR-615 | Run the standard scenario library and compare against the baseline | M | M4 |
| FR-616 | Compose scenarios respecting correlation structure | S | M4 |
| FR-617 | Evaluate user-posed counterfactuals ("what if I…?") | S | M4 |

## FR-7xx — Attribution and change

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-701 | Decompose any metric delta into classified drivers with exact-sum attribution | M | M4 |
| FR-702 | Classify each driver as Controllable, Structural, or Stochastic | M | M4 |
| FR-703 | Reject any attribution with residual above 2% of the total delta | M | M4 |
| FR-704 | Refuse to attribute across differing model versions | M | M4 |
| FR-705 | Label model-version and assumption changes as `ModelChange`, in a separate channel | M | M4 |
| FR-706 | Label late-arriving data corrections as `Restatement` | M | M4 |
| FR-707 | Apply the Signal Gate: class filter, materiality, significance, deduplication | M | M4 |
| FR-708 | Retain suppressed deltas, queryable and inspectable | M | M4 |
| FR-709 | Show a suppressed-movement summary in the Change Feed | M | M4 |
| FR-710 | Report Controllable Drift over 12 months | M | M4 |
| FR-711 | Rank drivers by contribution for any period | M | M4 |

## FR-8xx — Reliability, advice, calibration

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-801 | Define and evaluate the financial SLI catalogue | M | M4 |
| FR-802 | Track error budgets in the natural unit of each SLI | M | M4 |
| FR-803 | Compute the Discretionary Error Budget from the FI-date tolerance | M | M4 |
| FR-804 | Apply multi-window burn-rate alerting | M | M4 |
| FR-805 | Declare an incident after two consecutive SLO breaches, with a runbook | M | M4 |
| FR-806 | Require a postmortem within 7 days of incident resolution | M | M4 |
| FR-807 | Compute the Health Score with its component breakdown | M | M4 |
| FR-808 | Reject any SLI definition that market marks alone could move | M | M4 |
| FR-810 | Enumerate a feasible policy space from drivers, SLOs, sensitivities, and structure | M | M5 |
| FR-811 | Evaluate each option as a counterfactual on the baseline snapshot | M | M5 |
| FR-812 | Evaluate each option across the scenario set, reporting tail effects | M | M5 |
| FR-813 | Rank options by Pareto dominance, then by declared preference | M | M5 |
| FR-814 | Present 2–5 options, never one | M | M5 |
| FR-815 | Disclose assumptions, sensitivities, and a non-empty `notModelled` list | M | M5 |
| FR-816 | Report option cost after tax | M | M5 |
| FR-817 | Report "no detectable difference" when a delta is within Monte Carlo noise | M | M5 |
| FR-818 | Create a new Policy version when an option is accepted | M | M5 |
| FR-820 | Register every forecast for future scoring | M | M4 |
| FR-821 | Score resolved forecasts using Brier, CRPS, and PIT | M | M6 |
| FR-822 | Report calibration per horizon band and metric, never aggregated | M | M6 |
| FR-823 | Report `Unknown` where sample is insufficient | M | M6 |
| FR-824 | Suppress advice depending on a degraded reliability band, stating why | M | M6 |
| FR-825 | Detect calibration drift and open a model review, never auto-retune | M | M6 |
| FR-826 | Display reliability alongside the North Star | M | M6 |

## FR-9xx — Surfaces, progression, narrative, data

| ID | Requirement | Pri | Milestone |
|---|---|---|---|
| FR-901 | Present Mission Control with at most six cards, each stating its question | M | M4 |
| FR-902 | Present a Change Feed of gated events with a suppressed-movement summary | M | M4 |
| FR-903 | Present the SLO panel, incidents, and runbooks | M | M4 |
| FR-904 | Provide a bitemporal ledger explorer with as-of controls | S | M5 |
| FR-905 | Provide scenario comparison views | S | M5 |
| FR-906 | Provide calibration diagrams | S | M6 |
| FR-907 | Trace any displayed number to its producing artifact in one interaction | M | M4 |
| FR-910 | Track Discipline Index, streaks, XP, and Architecture Level from process events only | S | M6 |
| FR-911 | Suspend rather than break a streak on a recorded deliberate pause | S | M6 |
| FR-920 | Render narrative prose over a validated FactSet | S | M6 |
| FR-921 | Reject any narrative containing an untraceable numeric token | M | M6 |
| FR-922 | Label all generated content, with its model identifiable | M | M6 |
| FR-930 | Export the complete system to open, self-describing formats in one command | M | M2 |
| FR-931 | Re-import a full export into a clean database, verified in CI | M | M2 |
| FR-932 | Perform LGPD erasure by destroying the tenant data key | M | M5 |
| FR-940 | Present the full product surface in pt-BR and en, switchable at runtime, defaulting to pt-BR | M | M4 |
| FR-941 | Format numbers, currency, percentages, dates, and durations per locale, server-side | M | M4 |
| FR-942 | Preserve Tier-1 regulatory terms untranslated in every locale, with an English gloss on first use | M | M4 |
| FR-943 | Enforce banned-construction copy lints **per locale** | M | M5 |
| FR-944 | Generate narrative prose per locale rather than translating generated text | M | M6 |
| FR-945 | Localise rule-trace labels while preserving Portuguese legal citations | S | M5 |

---

## Traceability

| Layer | Link |
|---|---|
| FR → business rule | [Business Rules](../02-domain/05-business-rules.md) |
| FR → invariant | [Domain Model](../02-domain/04-domain-model.md) |
| FR → milestone | [Roadmap & Milestones](08-roadmap-and-milestones.md) |
| FR → acceptance test | [User Stories](10-user-stories.md), [Testing Strategy](../05-engineering/03-testing-strategy.md) |
