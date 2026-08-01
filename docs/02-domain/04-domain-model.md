# Domain Model

**Status:** Ratified · **Owner:** Domain · **Last reviewed:** 2026-08-01

Aggregates, entities, value objects, and their invariants. Notation is deliberately
language-neutral — this document constrains the code, not the other way round.

`INV-nnn` identifiers are permanent and referenced from tests. **An invariant without a test is a
defect.**

---

## 0. System-wide invariants

| ID | Invariant | Enforced where |
|---|---|---|
| **INV-001** | Monetary quantities are integers in minor units paired with a `Commodity`. Floating-point money is a compile-time impossibility | `Atlas.Kernel`, banned-type analyser |
| **INV-002** | Every ledger fact carries `ValidTime` and `DecisionTime`. No fact is ever updated in place | Ledger persistence layer |
| **INV-003** | Every aggregate root carries `TenantId` | Base type + persistence guard |
| **INV-004** | Every state change emits a domain event; no silent mutation | Aggregate base class |
| **INV-005** | Rounding is half-even, applied once, at a declared boundary; intermediate values keep full precision | `Money` arithmetic |
| **INV-006** | Any cross-commodity comparison requires an explicit `MarkSet` and instant. Implicit conversion is forbidden | `Money` operators |

---

## 1. Kernel value objects

### `Money`
```
Money := (amount: int64 minor units, commodity: Commodity)
```
- **INV-010** Arithmetic between different commodities throws. Conversion is explicit and requires a `MarkSet`.
- **INV-011** Division produces `(quotient, remainder)`. Remainder is never discarded — it is allocated by the largest-remainder method so that split amounts always re-sum to the original.
- **INV-012** `int64` minor units. BRL supports ±92 quadrillion centavos; overflow is a domain error, not a wrap.

### `Commodity`
```
Commodity := (symbol, kind, minorUnitScale, jurisdiction?)
kind ∈ { FiatCurrency, Equity, FIIQuota, FundQuota, FixedIncomeInstrument,
         PensionPlan, RealAsset, Crypto, Unmodelled }
```
- **INV-013** `Unmodelled` commodities are excluded from forecast distributions and reported separately (NG-10).

### `BitemporalInterval`
```
BitemporalInterval := (validFrom, validTo, decidedFrom, decidedTo)
```
- **INV-014** Both intervals are half-open `[from, to)`; `to = ∞` means currently true / currently believed.
- **INV-015** For a given fact key, valid-time intervals at any single decision-time do not overlap.

---

## 2. Ledger context

### Aggregate: `Account`
```
Account
  id, tenantId, code, name
  type ∈ { Asset, Liability, Equity, Income, Expense }
  commodity: Commodity        // accounts are single-commodity
  parentId?                   // chart of accounts is a tree
  openedAt, closedAt?
```
- **INV-020** An account's `type` is immutable after first posting.
- **INV-021** An account may not be closed while any non-zero balance remains at `decisionTime = now`.
- **INV-022** Account tree depth ≤ 6; codes are unique per tenant.

### Aggregate: `JournalEntry` *(the heart of the system)*
```
JournalEntry
  id, tenantId
  validTime            // when the economic event occurred
  decisionTime         // when Atlas learned it
  description, sourceRef, idempotencyKey
  postings: Posting[]  // ≥ 2
  correctsEntryId?     // set when this is a correction
  status ∈ { Posted, Reversed }

Posting := (accountId, money: Money, direction ∈ {Debit, Credit}, lotRef?)
```
- **INV-030** For each `Commodity` appearing in an entry, Σ debits = Σ credits. **Multi-commodity
  entries are permitted** (e.g. buying PETR4 with BRL) and each commodity balances independently
  via a trading/exchange account.
- **INV-031** An entry, once posted, is **immutable**. Errors are fixed by posting a reversal plus a
  replacement, both linked to the original.
- **INV-032** `idempotencyKey` is unique per `(tenantId, sourceId)`. Re-importing the same source
  data can never create a duplicate entry.
- **INV-033** `decisionTime` is assigned by the system and is monotonically non-decreasing.
- **INV-034** `validTime` may be in the past but never in the future beyond the market close of the
  current day.

### Read model: `LedgerBalance`
```
BalanceAt(accountId, asOfValidTime, asOfDecisionTime) → Money
```
- **INV-035** Every balance query takes **both** time coordinates. A single-time balance query does
  not exist in the API — this is how "what changed since yesterday" stays answerable when data
  arrives late.

---

## 3. Position & Valuation context

### Aggregate: `Position`
```
Position
  id, tenantId, commodity
  lots: Lot[]
  corporateActions: CorporateAction[]

Lot
  id, quantity: decimal, unitCost: Money, acquiredAt: date
  sourceEntryId, disposals: Disposal[]
  instrumentClass          // drives tax regime
Disposal := (quantity, proceeds: Money, disposedAt, entryId)
```
- **INV-040** `Σ lot.quantity − Σ disposal.quantity` equals the ledger quantity for that commodity at the same bitemporal coordinates. **Reconciliation is an invariant, not a report.**
- **INV-041** A disposal may not exceed remaining lot quantity.
- **INV-042** Corporate actions adjust quantity and unit cost such that **total cost basis is
  preserved** (splits/bonuses), or are recorded as a taxable event (mergers with cash).
- **INV-043** Brazilian equities use *custo médio* across all lots of a commodity; the `Lot`
  structure is retained for audit and for regimes that require lot identification (e.g. exterior).

### Value object: `Valuation`
```
Valuation := (positions, markSet, instant, total: Money, staleness: Duration[])
```
- **INV-044** A valuation records the **staleness of every mark used**. A valuation with any mark
  older than its tolerance is flagged, not hidden.
- **INV-045** Valuations are projections and are never persisted as truth — they are cached with
  their inputs' hashes and are always recomputable.

---

## 4. Taxation context (pure)

### Value object: `TaxLot`
```
TaxLot := (lotRef, regime: TaxRegime, acquiredAt, basis: Money,
           quantity, holdingPeriod, exemptionEligibility[])
```

### Value object: `TaxConsequence`
```
TaxConsequence := (
  taxableBase: Money, rate: Percentage, taxDue: Money,
  withheldAtSource: Money, netPayable: Money,
  exemptionsApplied: ExemptionClaim[],
  rulesetVersion: SemVer, explanation: RuleTrace[]
)
```
- **INV-050** Every `TaxConsequence` carries a `RuleTrace` — the ordered list of rules applied with
  their inputs. A tax figure that cannot explain itself is rejected.
- **INV-051** `rulesetVersion` and its effective date range are always recorded, so historical
  forecasts remain interpretable under the law as it stood.
- **INV-052** The tax module is **pure**: no I/O, no clock access, no randomness. Time is always a
  parameter. This is what makes decade-scale replay possible.

Full rule content: [Tax Engine — Brazil](../04-engines/07-tax-engine-brazil.md).

---

## 5. Cashflow & Behaviour context

### Aggregate: `SpendingModel`
```
SpendingModel
  id, tenantId, fittedAt, modelVersion
  categories: CategoryModel[]
  floor: SpendingFloor
  personalInflation: PersonalInflationIndex
  residualVolatility: Percentage

CategoryModel := (category, classification ∈ {Essential, CommittedDiscretionary, Discretionary},
                  process ∈ {Recurring, Seasonal, Episodic, Trending},
                  parameters, goodnessOfFit)
SpendingFloor := (essential: Money/month, committed: Money/month, asOf, realTerms: bool)
```
- **INV-060** `classification` is a **user decision**, versioned with an audit trail. The system may
  *propose* but never silently assign — it moves the FI number directly (hotspot H2).
- **INV-061** `SpendingFloor` is expressed in real terms with an explicit base date.
- **INV-062** `PersonalInflationIndex` is computed from the user's own basket, never assumed equal
  to IPCA. The divergence from IPCA is itself a displayed driver.
- **INV-063** Refitting the model emits `SpendingModelRefitted` with the prior version retained —
  so a change in the *model* is never mistaken for a change in *behaviour*.

---

## 6. Liabilities context

### Aggregate: `Loan`
```
Loan
  id, tenantId, principal: Money, originatedAt, termMonths
  system ∈ { SAC, Price, American, Custom }
  nominalRate, indexation ∈ { None, TR, IPCA, IGPM, CDI }
  insurance, fees, schedule: Instalment[]
  outstanding: Money, rateResets: RateReset[]
```
- **INV-070** The schedule is regenerated (never edited) after any rate reset, indexation, or extra
  amortisation; prior schedules are retained.
- **INV-071** Extra amortisation must declare its mode — *reduce term* or *reduce instalment* —
  because the FI impact differs materially.
- **INV-072** Every instalment payment reconciles to a ledger entry; unreconciled instalments raise
  a data-quality SLI breach.

---

## 7. Human Capital context

### Aggregate: `HumanCapitalValuation`
```
HumanCapitalValuation
  id, tenantId, asOf
  trajectory: EarningsTrajectory     // expected real earnings by year to retirement
  employmentRisk: {hazardRate, severanceMonths, expectedSearchDuration}
  marketBeta: decimal                // correlation of income shock with equity drawdown
  discountRate, presentValue: Money
```
- **INV-080** `marketBeta` is **mandatory and explicit**. Modelling human capital as uncorrelated
  with markets is the standard error that hides the dominant tail risk (job loss during a
  drawdown), and Atlas will not permit it.
- **INV-081** Human capital is reported separately and is **never** included in the FI portfolio
  balance — it is an input to the income process, not an asset that can be sold.

---

## 8. Goals & Policy context

### Aggregate: `Policy`
```
Policy
  id, tenantId, declaredAt, supersedesId?
  savingsRate: Percentage
  targetAllocation: AllocationBand[]      // (assetClass, min, target, max)
  contributionSchedule, debtStrategy ∈ {Avalanche, Snowball, MinimumOnly, Custom}
  wrapperPreference[]                     // PGBL/VGBL/taxable/LCI-LCA ordering
  withdrawalSequence: AccountRef[]
  rebalancingRule, confidenceTarget: Percentage
  horizonAge: int
```
- **INV-090** A `Policy` is immutable; changing it creates a new version linked via `supersedesId`.
- **INV-091** `AllocationBand` min ≤ target ≤ max, and targets sum to 100%.
- **INV-092** Every `Policy` version records **why** it changed — user-initiated, or accepted
  Advisory option (with the `OptionId`). This closes the intention→action loop (R33).

### Aggregate: `Goal`
```
Goal
  id, tenantId, name, targetAmount: Money, targetDate
  priority: int, fundingAccounts[], flexibility ∈ {Hard, Soft, Aspirational}
  status ∈ {Active, Achieved, Deferred, Abandoned}
```
- **INV-100** Priorities are a **total order** — no ties. Shortfall must be attributable to a named
  goal, not smeared (hotspot H5).
- **INV-101** `Hard` goals are modelled as constraints in the forecast; `Soft` and `Aspirational`
  as objectives. The distinction changes the maths, not just the label.

---

## 9. Digital Twin context

### Aggregate: `TwinSnapshot` *(the critical seam)*
```
TwinSnapshot
  hash: SHA-256                 // content address, the identity
  takenAt, schemaVersion, tenantId
  positionBook, spendingModel, incomeModel, liabilitySchedules
  humanCapital, policy, goals, marketState        // curves, indices, staleness
  taxState                                        // YTD realised gains, exemption usage
  provenance: SourceRef[]
```
- **INV-110** **Completeness.** A forecast must be computable from the snapshot alone. Any engine
  read outside the snapshot is a defect.
- **INV-111** **Immutability.** Snapshots are never modified. Corrections create new snapshots.
- **INV-112** **Determinism of hash.** Canonical serialisation (sorted keys, fixed number format,
  UTC). Identical financial state ⇒ identical hash, on any machine, in any year.
- **INV-113** **Perpetual readability.** Every historical `schemaVersion` remains deserialisable
  for the life of the project. Deleting a snapshot reader is forbidden.

---

## 10. Forecast context

### Aggregate: `ForecastArtifact`
```
ForecastArtifact
  id, tenantId
  twinSnapshotHash, modelVersion: SemVer, assumptionSetHash, seed: uint64
  computedAt, horizon
  outcomes: { fiDateDistribution, ruinProbability, terminalWealthDistribution,
              goalAchievementProbabilities[], pathSummaries }
  modelUncertainty: {ensembleSpread, parameterSensitivity[]}
  computeMetadata: {pathCount, durationMs, engineBuild}
```
- **INV-120** Artifacts are **immutable and never deleted**.
- **INV-121** `(twinSnapshotHash, modelVersion, assumptionSetHash, seed)` is a **unique key** and
  fully determines the outcome. Re-running must reproduce it bit-for-bit; a CI test asserts this.
- **INV-122** Every distribution reports **at minimum** P10/P50/P90 and its own `modelUncertainty`.
  A bare point estimate cannot be constructed from the type.
- **INV-123** Two artifacts may only be compared if `modelVersion` matches. Cross-version deltas are
  labelled `ModelChange`, never presented as a change in the user's situation. **This is what makes
  "what changed since yesterday" honest.**

---

## 11. Attribution context

### Value object: `Attribution`
```
Attribution
  metric ∈ { FIDate, PFIByTarget, HealthScore, GoalProbability }
  fromArtifactId, toArtifactId, totalDelta
  drivers: DriverContribution[]
  unexplainedResidual                       // must be small; large residual is a defect
  classification: {controllable, structural, stochastic}   // sums to totalDelta

DriverContribution := (driver, class: DriverClass, contribution, shapleyShare, confidence)
```
- **INV-130** `Σ driver.contribution + unexplainedResidual = totalDelta`, exactly.
- **INV-131** `|unexplainedResidual| ≤ 2%` of `|totalDelta|`, else the attribution is rejected and
  an engineering alert (not a user alert) is raised.
- **INV-132** Drivers of class `Stochastic` **can never produce a `ChangeEvent`**. Structurally
  enforced: the Signal Gate's input type excludes them.
- **INV-133** Attribution requires `modelVersion` equality between artifacts (INV-123).

### Value object: `SignalGate`
A delta becomes a `ChangeEvent` only if **all** hold:
1. It contains at least one `Controllable` or `Structural` driver, **and**
2. that driver's contribution exceeds its materiality threshold (default: ≥ 30 FI-days or ≥ 1pp of `P(FI)`), **and**
3. it is statistically distinguishable from the metric's own noise band (default: > 2σ of the trailing 90-day stochastic distribution), **and**
4. it is not a duplicate of an open `ChangeEvent` for the same driver within the suppression window.

- **INV-134** Failing the gate produces `SignalGateSuppressed` — retained, queryable, never
  surfaced by default. **Nothing is deleted; things are simply not shouted about.**

---

## 12. Financial Reliability context

### Aggregate: `FinancialSLO`
```
FinancialSLO
  id, tenantId, sliId, target, comparator, window
  errorBudget: {unit, allowance, consumed}
  burnRateThresholds: {fast, slow}
  severity ∈ {SEV1..SEV4}
```
- **INV-140** An SLO's **unit is the natural unit of the thing measured** — months of coverage,
  R$ of overspend, percentage points of drift. Never an abstract score.
- **INV-141** An SLO whose SLI can be moved by market marks alone is invalid and rejected at
  definition time (Law 11 / Law 2).
- **INV-142** Two consecutive breach evaluations ⇒ `IncidentDeclared` with a linked `Runbook`.
- **INV-143** `IncidentResolved` requires a `Postmortem` within 7 days.

---

## 13. Advisory context

### Aggregate: `Option`
```
Option
  id, tenantId, generatedAt, expiresAt
  policyDelta                       // the precise change from π₀
  evaluation: { fiDateDelta: {p10,p50,p90}, pFIDelta, ruinProbabilityDelta,
                afterTaxCost: Money, liquidityImpact, reversibility, effortRequired }
  ranking: {rank, dominatedBy[], paretoFrontMember: bool}
  disclosure: { assumptions[], notModelled[], sensitivities[], reliabilityCaveat? }
  status ∈ {Presented, Accepted, Dismissed, Expired}
```
- **INV-150** `evaluation` is produced by running a **counterfactual forecast on the same
  `TwinSnapshot`** as the baseline. Comparing against a different snapshot is forbidden.
- **INV-151** `disclosure.notModelled` is **mandatory and non-empty**. Atlas must always state what
  it does not know (Law 14).
- **INV-152** Options are ranked, never singular. Minimum 2, maximum 5 presented.
- **INV-153** If `ReliabilityStatus` for the relevant horizon is `Degraded`, options depending on it
  are suppressed with a stated reason (Seam C).
- **INV-154** `afterTaxCost` must route through `ITaxJurisdiction`. Pre-tax option costs are a
  build-failing defect (Law 12).

---

## 14. Progression context

### Aggregate: `ProgressionProfile`
```
ProgressionProfile
  id, tenantId
  streaks: Streak[], disciplineIndex, operationalXP, architectureLevel
  eligibleEventTypes: ProcessEventType[]      // allow-list
```
- **INV-160** The module's compile-time dependency set **excludes** Valuation, Forecast, and Market
  Data. It cannot observe returns even by accident (Law 11).
- **INV-161** XP is awarded only for events on the `eligibleEventTypes` allow-list, all of which are
  user-controllable process actions.
- **INV-162** A streak may be broken only by a user action or inaction — **never** by a market
  event, a price move, or a forecast change.

---

## 15. Narrative context

### Value object: `FactSet`
```
FactSet := (facts: Fact[], generatedAt, sourceArtifactRefs[])
Fact := (id, label, value, unit, provenance: ArtifactRef, confidence?)
```
- **INV-170** Narrative's dependency set contains **only** `FactSet` contracts. It cannot reach
  Ledger, Forecast, or Tax types.
- **INV-171** Every numeric token in rendered output must resolve to a `Fact.id`. Unresolvable
  tokens ⇒ `NarrativeRejected` ⇒ fall back to structured display. Enforced at generation time.
- **INV-172** The narrative layer may not perform arithmetic, including rounding or unit conversion.
  Pre-formatted display strings are supplied in the `FactSet`.

---

**See also:** [Business Rules](05-business-rules.md) · [Context Map](03-context-map.md) · [Data Strategy](../03-architecture/04-data-strategy.md)
