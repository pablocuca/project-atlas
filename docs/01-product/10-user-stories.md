# User Stories & Acceptance Criteria

**Status:** Ratified · **Owner:** Product

Gherkin-level detail for M0–M2, plus the stories that define the product's character in M3–M6.
Every story cites its FR and its business rules. **Acceptance criteria are the specification** —
where prose and criteria disagree, the criteria win.

---

## M0 — Ledger foundation

### US-001 · Record a transaction manually
> **As** the Operator, **I want to** record a financial transaction with full detail, **so that**
> the ledger reflects reality even with no integrations. `FR-101, FR-103`

```gherkin
Scenario: A balanced two-posting entry
  Given accounts "Assets:Bank:Itau" and "Expenses:Groceries"
  When I record R$ 250,00 from Itau to Groceries dated 2026-08-01
  Then a journal entry is posted with two postings
  And the sum of postings for BRL is exactly zero
  And valid_time is 2026-08-01
  And decision_time is the current instant

Scenario: An unbalanced entry is rejected
  When I attempt an entry with a single R$ 100,00 debit
  Then the entry is rejected with DomainError "ENTRY_UNBALANCED"
  And nothing is persisted

Scenario: Multi-commodity entry balances per commodity
  When I record buying 100 PETR4 at R$ 38,50 paid from Itau
  Then the BRL postings sum to zero
  And the PETR4 postings sum to zero
  And a trading account absorbs the exchange
```

### US-002 · Query a balance at any point in both timelines
> **As** the Operator, **I want to** ask what a balance was at a past date *as I understood it then*,
> **so that** I can distinguish real change from late-arriving information. `FR-105, BR-102`

```gherkin
Scenario: Both time coordinates are required
  When I call BalanceAt with only a valid time
  Then the code does not compile
  # There is deliberately no single-time overload (INV-035)

Scenario: Belief at a past instant
  Given an entry for 2026-07-01 recorded on 2026-07-02
  And a correction for the same event recorded on 2026-07-23
  When I query balance at valid_time 2026-07-15, decision_time 2026-07-10
  Then the result reflects the ORIGINAL entry only
  When I query balance at valid_time 2026-07-15, decision_time 2026-08-01
  Then the result reflects the CORRECTED entry
```

### US-003 · Correct a past entry without destroying history
> `FR-106, BR-101`

```gherkin
Scenario: Correction preserves the original
  Given a posted entry E1 for R$ 250,00 on 2026-07-01
  When I correct it to R$ 275,00
  Then E1 remains present and unmodified
  And a reversal entry is posted referencing E1
  And a replacement entry is posted referencing E1
  And all three share the same valid_time
  And the current balance reflects R$ 275,00

Scenario: Direct mutation is impossible
  When any code path attempts to update a posted entry
  Then the database rejects it — the role has no UPDATE on ledger truth tables
```

---

## M1 — Ingestion

### US-010 · Import a bank CSV
> `FR-107, FR-109`

```gherkin
Scenario: First import
  Given a CSV of 180 transactions with a saved column mapping
  When I import it
  Then the raw payload is archived to blob BEFORE parsing
  And 180 entry proposals are created
  And each has an idempotency key derived from the raw record

Scenario: Overlapping re-import creates no duplicates
  Given the previous import is confirmed
  When I import a CSV overlapping by 30 transactions
  Then 30 rows are recognised as already imported
  And exactly 150 new proposals are created

Scenario: Malformed rows do not lose the batch
  Given a CSV where rows 45 and 46 have an unparseable date
  When I import it
  Then 178 rows parse successfully
  And 2 parse failures are recorded with row detail
  And the raw payload remains recoverable for a future parser fix
```

### US-011 · Resolve a probable duplicate across sources
> `FR-110`

```gherkin
Scenario: Fuzzy match is queued, never auto-merged
  Given a manual entry of R$ 1.200,00 to "João" on 2026-07-14
  When a bank import contains R$ 1.200,00 PIX to "JOAO S" on 2026-07-15
  Then a duplicate candidate is raised with similarity ≥ 0.85
  And NEITHER record is merged automatically
  And I am asked to resolve it
```

### US-012 · Reconcile a source
> `FR-111, BR-108`

```gherkin
Scenario: Reconciliation within tolerance
  Given the bank statement reports R$ 14.328,91 on 2026-07-31
  When the ledger-derived balance is R$ 14.328,91
  Then the source is marked reconciled at that date

Scenario: Drift raises a data-quality breach, never a silent fix
  Given the statement reports R$ 14.328,91
  And the ledger derives R$ 14.290,00
  Then a ReconciliationDiscrepancy of R$ 38,91 is raised
  And the ReconciliationDrift SLI breaches
  And NO adjusting entry is created automatically
```

### US-013 · Classify expenses as essential or discretionary
> `FR-301, INV-060`

```gherkin
Scenario: Classification is an explicit, versioned decision
  Given category "Restaurantes" is unclassified
  When Atlas proposes "Discretionary" with rationale
  Then it remains unclassified until I confirm
  And on confirmation a versioned decision record is created with timestamp

Scenario: Reclassification is visible in attribution
  Given "Plano de saúde" was classified Discretionary
  When I reclassify it as Essential
  Then the SpendingFloor increases
  And the resulting FI-date change is attributed to driver "SpendingClassification"
  And it is classed Controllable, not Structural
```

---

## M2 — Valuation, liabilities, export

### US-020 · Apply a stock split preserving basis
> `FR-203, INV-042`

```gherkin
Scenario: 1:2 split
  Given 100 shares of X with total basis R$ 3.850,00
  When a 1:2 split is applied
  Then the position is 200 shares
  And the total basis remains exactly R$ 3.850,00
  And the unit cost is exactly R$ 19,25
  And no taxable event is recorded
```

### US-021 · FII amortisation reduces basis, not income
> `FR-204`

```gherkin
Scenario: Amortisation is not a distribution
  Given 100 quotas of an FII with total basis R$ 10.000,00
  When an amortisation of R$ 2,00 per quota is received
  Then R$ 200,00 cash is recorded
  And the total basis becomes R$ 9.800,00
  And NO income event is recorded
  # A frequent and costly error in Brazilian FII accounting
```

### US-022 · Model a SAC mortgage matching the lender
> `FR-307, FR-308`

```gherkin
Scenario: Schedule matches the statement
  Given a SAC loan of R$ 400.000,00 over 360 months, TR-indexed
  When the schedule is generated
  Then each instalment matches the lender's statement to the centavo for 12 months

Scenario: Extra amortisation requires an explicit mode
  When I record an extra amortisation of R$ 20.000,00
  Then I must specify "reduce term" or "reduce instalment"
  And a new schedule is generated
  And the prior schedule is retained
  And the FI-date impact differs measurably between the two modes
```

### US-023 · Export everything and restore it
> `FR-930, FR-931, NFR-704`

```gherkin
Scenario: Full export
  When I run `atlas export --full --out ./export`
  Then ledger, snapshots, artifacts, calibration, intent, market data and raw payloads are written
  And MANIFEST.json records schema versions and hashes
  And SCHEMA.md describes every field and unit
  And every file is Parquet, JSON, CSV, or zstd — nothing proprietary

Scenario: Restore into a clean database (runs in CI)
  Given a full export
  When it is imported into an empty database
  Then every ledger entry, snapshot hash, and artifact hash is identical
  And the assertion runs on every pipeline execution
```

---

## M3–M4 — The stories that define the product

### US-030 · Come-cotas is a discrete event, not an annual haircut
> `FR-504, BR-404`

```gherkin
Scenario: May withholding
  Given a multimercado fund position held since 2025-01
  When the forecast reaches May of any simulated year
  Then come-cotas withholding is computed on the gain since the last event
  And it is taken in quotas, reducing quota count
  And it appears in the RuleTrace with its ruleset version
  And it is NOT modelled as an annualised percentage drag
```

### US-031 · A forecast replays bit-identically
> `FR-612, BR-302, NFR-102`

```gherkin
Scenario: Determinism across environments
  Given artifact A produced from (snapshotHash S, model 2.1.0, assumptions H, seed 42)
  When the same key is re-run on different hardware, OS, and .NET patch version
  Then the output bytes are identical to A
  And the CI determinism gate asserts this on every pipeline run
```

### US-032 · Job loss in a crash is worse than job loss alone
> `FR-614, INV-080`

```gherkin
Scenario: Correlation is genuinely modelled
  Given a twin with humanCapital.marketBeta > 0
  When I run scenario JobLoss and scenario JobLossInCrash
  Then JobLossInCrash shows a materially higher ruin probability
  And the difference exceeds Monte Carlo noise
  # If these are equal, the correlation is not implemented — this test must fail
```

### US-033 · A market move alone never notifies me
> `FR-707, BR-210` — **the product's defining behaviour**

```gherkin
Scenario: Stochastic-only delta is suppressed
  Given no change to income, spending, policy, goals, or liabilities
  When equity marks move the FI date by 19 days
  Then the delta is attributed entirely to Stochastic drivers
  And a SignalGateSuppressed event is recorded
  And NO ChangeEvent is created
  And NO notification is dispatched
  And the delta remains inspectable in the variance view

Scenario: The gate cannot be bypassed
  When any code path attempts to construct a ChangeEvent from a stochastic-only attribution
  Then it does not compile — the input type excludes it
```

### US-034 · A salary change does notify me, with its cause
> `FR-707, FR-709`

```gherkin
Scenario: Controllable driver passes the gate
  Given my primary income rises 8.4%
  When the forecast recomputes
  Then the FI-date delta is attributed principally to driver "IncomeChange", class Controllable
  And it exceeds 30 FI-days
  And it exceeds 2σ of the trailing 90-day noise band
  Then a ChangeEvent is raised with the driver named and quantified
  And exactly one notification is dispatched
```

### US-035 · The Change Feed tells me what it hid
> `FR-709`

```gherkin
Scenario: Suppression is disclosed, never invisible
  Given 14 deltas were suppressed in the last 30 days
  When I open the Change Feed
  Then a summary row shows "14 suppressed movements (market variance) ±31 days"
  And tapping it lists each with its full attribution
```

### US-036 · An empty Change Feed is good news
> `UX-7`

```gherkin
Scenario: Emptiness presented affirmatively
  Given no ChangeEvents in the last 30 days
  When I open the Change Feed
  Then it states "No attributable changes. Your trajectory is stable."
  And there is no empty-state illustration
  And there is no prompt to take any action
```

---

## M5–M6 — Advice and self-assessment

### US-040 · Options, never instructions
> `FR-814, BR-601, BR-604`

```gherkin
Scenario: Minimum two options
  When Atlas identifies an improvement opportunity
  Then at least 2 and at most 5 options are presented
  And no option text contains "you should", "buy", "sell", or "act now"
  And the copy lint fails the build if any does

Scenario: Disclosure is mandatory and real
  Then every option lists assumptions, sensitivities, and a non-empty "not modelled" list
  And the "not modelled" entries derive from actual gaps in the twin and model
```

### US-041 · Atlas admits when it is not reliable enough to advise
> `FR-824, BR-606` — **the humility mechanism**

```gherkin
Scenario: Degraded reliability suppresses dependent advice
  Given ForecastReliability for the 1–5 year band is Degraded
  When allocation options would be generated
  Then they are suppressed
  And Atlas states: which band, since when, that a model review is open,
      and which option classes are unaffected
  And cashflow options continue to be presented
```

### US-042 · Calibration reports ignorance honestly
> `FR-822, FR-823, BR-705`

```gherkin
Scenario: Insufficient sample never defaults to good
  Given 63 resolved 1-month claims and 1 resolved 3-year claim
  When calibration is reported
  Then the 1-month band shows a score with n=63
  And the 3-year band shows "Unknown — insufficient sample"
  And the 10-year band shows "Unverified — extrapolation"
  And NO aggregate score across bands is produced
```

### US-043 · A market crash cannot break my streak
> `FR-911, BR-802, INV-162`

```gherkin
Scenario: Progression cannot observe markets
  Given a 14-month contribution streak
  When the portfolio falls 30%
  Then the streak remains 14 months
  And the Discipline Index is unchanged
  And the Health Score is unchanged
  # Progression has no compile-time dependency on Valuation (MR-8)

Scenario: A recorded pause suspends rather than breaks
  Given I record a deliberate contribution pause due to job loss
  Then the streak is suspended, not broken
  And it resumes on the next qualifying contribution
```

### US-044 · Generated prose cannot invent a number
> `FR-921, BR-901`

```gherkin
Scenario: Untraceable token is rejected at generation time
  Given a FactSet containing facts F1..F7
  When the narration produces a numeric token not resolving to any Fact.id
  Then NarrativeRejected is emitted
  And the structured display is shown instead
  And no generated text reaches the user
  And the rejection is counted in atlas_llm_narration_rejected_total
```

---

## Definition of Ready

A story may not enter development until:
- [ ] It cites at least one `FR-`
- [ ] Acceptance criteria are written in Gherkin
- [ ] Affected `BR-` and `INV-` identifiers are listed
- [ ] It is smaller than XL
- [ ] It violates no Product Law
- [ ] Its milestone's predecessor gate has passed

---

**See also:** [Epics & Backlog](09-epics-and-backlog.md) · [Business Rules](../02-domain/05-business-rules.md) · [Testing Strategy](../05-engineering/03-testing-strategy.md)
