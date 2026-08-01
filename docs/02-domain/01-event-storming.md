# Event Storming

**Status:** Ratified · **Owner:** Domain · **Last reviewed:** 2026-08-01

The complete event surface of a financial life, in past tense, grouped by the phase in which they
were discovered. Events are the **spine of the system**: every projection, forecast, score, and
alert is derived from this stream and nothing else.

**Notation:** 🟧 Domain Event · 🟦 Command · 🟨 Aggregate · 🟪 Policy (reactive rule) · 🟩 Read Model · 🟥 Hotspot / unresolved

---

## Phase 1 — Truth: facts entering the system

### Ingestion & reconciliation

| 🟦 Command | 🟨 Aggregate | 🟧 Event |
|---|---|---|
| `RegisterSource` | Source | `SourceRegistered` |
| `ImportStatement` | ImportBatch | `StatementImported`, `ImportRejected` |
| `ParseStatementRows` | ImportBatch | `RowsParsed`, `RowParseFailed` |
| `ProposeJournalEntries` | ImportBatch | `EntriesProposed` |
| `ConfirmProposal` | ImportBatch | `ProposalConfirmed`, `ProposalRejected` |
| `ReconcileSource` | Reconciliation | `SourceReconciled`, `ReconciliationDiscrepancyDetected` |
| `RecordSourceBalance` | Source | `SourceBalanceObserved` |

🟪 *On `EntriesProposed` where confidence ≥ threshold and amount below auto-confirm limit → auto-confirm.*
🟥 **Hotspot:** duplicate detection across overlapping statement windows. Resolved by ingestion idempotency keys — see [Ingestion](../03-architecture/05-ingestion-and-integration.md).

### Ledger

| 🟦 Command | 🟨 Aggregate | 🟧 Event |
|---|---|---|
| `PostJournalEntry` | JournalEntry | `JournalEntryPosted` |
| `CorrectJournalEntry` | JournalEntry | `JournalEntryCorrected` (emits reversal + replacement) |
| `OpenAccount` | Account | `AccountOpened` |
| `CloseAccount` | Account | `AccountClosed` |
| `RestateFact` | JournalEntry | `FactRestated` (new DecisionTime, same ValidTime) |

🟥 **Hotspot:** a broker corrects a trade from three weeks ago. Resolved by bitemporality — the
original entry is never touched; a restatement is appended, and every downstream projection can be
recomputed at either DecisionTime.

### Positions and market data

| 🟦 Command | 🟨 Aggregate | 🟧 Event |
|---|---|---|
| `RecordTrade` | Position | `LotAcquired`, `LotDisposed`, `LotSplit` |
| `RecordCorporateAction` | Position | `SplitApplied`, `BonusSharesIssued`, `TickerRenamed`, `MergerApplied` |
| `RecordDistribution` | Position | `DividendReceived`, `JCPReceived`, `RendimentoReceived`, `AmortizationReceived` |
| `IngestMark` | Commodity | `MarkObserved`, `MarkStale` |
| `RevalueHoldings` | Valuation | `ValuationComputed` |

🟥 **Hotspot:** cost basis after splits, bonuses, and mergers. Brazilian *custo médio* rules differ
per instrument class. Owned by the [Tax Engine](../04-engines/07-tax-engine-brazil.md).

---

## Phase 2 — Life: the human system

### Income and human capital

🟧 `SalaryReceived` · `SalaryChanged` · `BonusReceived` · `ThirteenthSalaryReceived` · `FGTSDeposited` ·
`EmploymentStarted` · `EmploymentEnded` · `EmploymentRiskReassessed` · `CareerTrackChanged` ·
`SideIncomeReceived` · `HumanCapitalRevalued`

🟪 *On `EmploymentEnded` → open a `FinancialIncident` of class `IncomeLoss` and re-forecast immediately with the JobLoss scenario promoted to base.*

### Spending and behaviour

🟧 `ExpenseRecorded` · `ExpenseCategorised` · `ExpenseReclassified` · `RecurringExpenseDetected` ·
`RecurringExpenseEnded` · `SpendingModelRefitted` · `PersonalInflationRecomputed` ·
`DiscretionaryBudgetBurned` · `SpendingFloorRevised`

🟥 **Hotspot:** *essential* vs *discretionary* is a judgement, not a fact, and it drives the FI
number directly. Resolved by making classification an explicit, versioned user decision with an
audit trail — never inferred silently.

### Liabilities

🟧 `LoanOriginated` · `AmortisationScheduleGenerated` · `InstalmentPaid` · `ExtraAmortisationMade` ·
`RateResetApplied` · `IndexationApplied` · `LoanSettled` · `LoanRefinanced`

### Family and life events

🟧 `DependentAdded` · `DependentRemoved` · `LifeEventPlanned` · `LifeEventOccurred` ·
`LifeEventCancelled` · `HealthStatusChanged` · `HousingSituationChanged`

---

## Phase 3 — Intent: goals and policy

🟧 `GoalDefined` · `GoalPrioritised` · `GoalFunded` · `GoalAchieved` · `GoalAbandoned` · `GoalDeferred` ·
`TargetDateSet` · `ConfidenceTargetSet` · `RiskToleranceAssessed` · `InvestmentPolicyStatementAdopted` ·
`PolicyDeclared` · `PolicyDeviationObserved` · `WithdrawalSequenceDeclared`

🟥 **Hotspot:** goals compete for the same capital. Resolved by making goal funding an explicit
allocation with priority ordering, so that any shortfall is attributed to a named goal rather than
smeared across all of them.

---

## Phase 4 — Foresight: the twin and its futures

🟧 `TwinSnapshotTaken` · `TwinStateRebuilt` · `ForecastRequested` · `ForecastComputed` ·
`ForecastArtifactStored` · `ForecastFailed` · `ModelVersionPublished` · `ModelVersionRetired` ·
`ScenarioDefined` · `ScenarioRun` · `CounterfactualEvaluated` · `AssumptionSetRevised`

🟪 *On `JournalEntryPosted` affecting a material account, or `SalaryChanged`, or `SpendingFloorRevised`, or a scheduled daily tick → `ForecastRequested`.*

🟥 **Hotspot:** re-forecasting on every posting is wasteful and creates spurious deltas. Resolved by
a **materiality gate**: only state changes exceeding a threshold effect on `t_FI` trigger a
recompute; everything else waits for the daily tick.

---

## Phase 5 — Meaning: change, signal, and attribution

🟧 `DeltaMeasured` · `DeltaAttributed` · `DriverRanked` · `SignalGatePassed` · `SignalGateSuppressed` ·
`ChangeEventRaised` · `VarianceBudgetConsumed` · `NoiseClassified`

🟪 *On `ForecastComputed` → measure Δ against the prior comparable forecast → attribute → gate.*
🟪 *On `SignalGateSuppressed` → record as noise, consume variance budget, emit nothing to the user.*

**This is the most important policy chain in the system.** It is what separates Atlas from a
volatility-amplification machine.

---

## Phase 6 — Reliability: the SRE layer

🟧 `SLIComputed` · `SLOBreached` · `SLORecovered` · `ErrorBudgetConsumed` · `ErrorBudgetExhausted` ·
`BurnRateAlertRaised` · `IncidentDeclared` · `IncidentAcknowledged` · `IncidentMitigated` ·
`IncidentResolved` · `PostmortemPublished` · `RunbookExecuted` · `HealthScoreRecomputed`

🟪 *On two consecutive `SLOBreached` for the same SLI → `IncidentDeclared`.*
🟪 *On `IncidentResolved` → require `PostmortemPublished` within 7 days, else raise a process incident.*

---

## Phase 7 — Decision: options and advice

🟧 `PolicySpaceEnumerated` · `OptionEvaluated` · `OptionsRanked` · `OptionPresented` ·
`OptionAccepted` · `OptionDismissed` · `OptionExpired` · `AdviceSuppressed`

🟪 *On `ForecastReliability` SLO breach → `AdviceSuppressed` for all option classes dependent on the degraded model. Atlas states why.*

🟥 **Hotspot:** an accepted option must become a *policy change*, which must be observable in
behaviour. Resolved by linking `OptionAccepted` → `PolicyDeclared` → later `PolicyDeviationObserved`,
closing the loop between intention and action.

---

## Phase 8 — Learning: calibration

🟧 `ForecastOutcomeObserved` · `ForecastScored` · `CalibrationCurveUpdated` · `ModelDriftDetected` ·
`ReliabilityDegraded` · `ReliabilityRestored`

🟪 *On the maturity date of any stored forecast horizon → observe the realised outcome → score → update calibration.*

**This loop runs forever and never faces the user except as a single number.** It is the reason the
North Star can be trusted.

---

## Phase 9 — Progression and communication

🟧 `ProcessActionCompleted` · `XPAwarded` · `StreakExtended` · `StreakBroken` · `LevelAchieved` ·
`DisciplineIndexRecomputed`
🟧 `BriefingGenerated` · `NarrativeRendered` · `NarrativeRejected` · `NotificationDispatched` · `NotificationSuppressed`

🟪 *On `NarrativeRendered` where any sentence lacks a traceable fact reference → `NarrativeRejected`, fall back to structured output.*

---

## Read models identified

🟩 `MissionControlSummary` · `ChangeFeed` · `DriverRanking` · `SLOPanel` · `IncidentList` ·
`FIProjectionBand` · `ScenarioComparison` · `RankedOptions` · `LedgerJournal` · `PositionBook` ·
`TaxYearSummary` · `CalibrationReport` · `ProgressionProfile`

---

## Aggregates identified

`Source` · `ImportBatch` · `Account` · `JournalEntry` · `Position` · `Commodity` · `Loan` ·
`IncomeStream` · `SpendingModel` · `Goal` · `Policy` · `TwinSnapshot` · `ForecastArtifact` ·
`Scenario` · `SLI` · `Incident` · `Option` · `CalibrationRecord` · `ProgressionProfile`

Each is assigned to exactly one bounded context in [Bounded Contexts](02-bounded-contexts.md).

---

## Unresolved hotspots carried forward

| # | Hotspot | Owner | Resolution vehicle |
|---|---|---|---|
| H1 | Cost basis across corporate actions | Tax Engine | [Tax Engine spec §6](../04-engines/07-tax-engine-brazil.md) |
| H2 | Essential vs discretionary classification | Product | Explicit user decision, versioned |
| H3 | Materiality gate thresholds | Forecast Engine | Calibrated empirically in M4 |
| H4 | Human capital correlation with equity markets | Forecast Engine | [Forecast Engine §5](../04-engines/02-forecast-engine.md) |
| H5 | Goal competition for capital | Goals context | Priority-ordered funding |
| H6 | Attribution when drivers interact non-linearly | Attribution Engine | Shapley decomposition |
