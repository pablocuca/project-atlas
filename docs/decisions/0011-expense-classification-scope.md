# Decision 0011 — Expense classification implements only Category classification, not all of C9

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [C9](../02-domain/02-bounded-contexts.md),
[FR-301](../01-product/02-functional-requirements.md), [INV-060](../02-domain/04-domain-model.md),
[US-013](../01-product/10-user-stories.md)

## Context

FR-301 ("classify each expense category as Essential / CommittedDiscretionary / Discretionary,
versioned with an audit trail") is one line item inside the M1 scope table, but its bounded context —
C9, Cashflow & Behaviour — owns a much larger aggregate: `SpendingModel` (`IncomeStream`,
`ExpenseRecord`, `Category`, `RecurringPattern`, `SpendingModel`, `PersonalInflationIndex`,
`SpendingFloor`). The domain model's own note calls `SpendingModel` "a *statistical* object — a
fitted, non-stationary process, not a budget." None of that fitting/forecasting machinery, or a
concept of `ExpenseRecord`/`IncomeStream` distinct from what Ledger already records, exists yet, and
building it isn't what FR-301 or the M1 exit gate ask for.

A second question: what *is* a "category" in a codebase that has no `Category` entity yet? Ledger
already has named `Expense`-type accounts (`atlas-seed` and every ingestion test already create ones
like "Restaurantes"-equivalent categories) — inventing a second, parallel taxonomy would duplicate
Ledger's chart of accounts for no reason FR-301 requires.

A third: US-013's Gherkin describes a *propose* step ("Atlas proposes 'Discretionary' with
rationale... it remains unclassified until I confirm") ahead of the *confirm* step that actually
creates the versioned record. No document anywhere specifies what the proposal heuristic should be —
unlike fuzzy duplicate detection (FR-110) or reconciliation tolerance (FR-111), which had concrete
worked examples to build against, there is no algorithm to classify a category as probably
Essential vs. Discretionary.

## Decisions

**This slice implements only Category classification** — a new `Cashflow` module (C9's name, since
future FR-3xx work belongs in the same module) containing exactly `ClassificationDecision` and its
CRUD-minus-U-and-D repository. `IncomeStream`, `ExpenseRecord`, `RecurringPattern`, `SpendingModel`,
`PersonalInflationIndex`, and `SpendingFloor` are not modelled at all — not stubbed, not deferred
fields on an aggregate, simply absent until a future slice actually needs them.

**A category is exactly a Ledger `Expense`-type account.** `ClassifyCategoryHandler` validates the
target account through a new OHS port, `Ledger.Contracts.IFindAccount` (the fourth such port,
following `IPostJournalEntry`/`IFindEntriesInRange`/`IQueryLedgerBalance`'s established shape:
Contracts interface → Application adapter wrapping `IAccountRepository` → registration in
`LedgerModule`), and rejects anything that isn't `AccountType.Expense`.

**No auto-proposal is implemented.** This slice covers only US-013's *confirm* half — a human (or a
future caller) submits a classification directly via `POST /cashflow/categories/{id}/classify`.
INV-060 already makes this legitimate on its own terms: *"the system **may** propose but never
silently assign"* — proposing is optional, confirming is the operative act that creates the record.
Building a proposal heuristic without a specified algorithm to build it against would be guesswork
Decision 0006 already argued against for a different feature (CSV import's CONFIRM stage).

**`ClassificationDecision` is append-only, enforced at the database level** — `cashflow.
classification_decision` grants `SELECT, INSERT` only, no `UPDATE`/`DELETE`, the same pattern
Ingestion's `import_batch` and `reconciliation` tables already established. A category's current
classification is simply its most recent decision by `DecidedAt` (`ClassificationHistoryExtensions.
Current()`) — there is no separate "current state" column to keep in sync, mirroring how Ledger has
no mutable balance column either.

## Consequences

- Reclassifying a category (US-013's second scenario) is just recording a second
  `ClassificationDecision` for the same `categoryAccountId` — the original decision is untouched and
  remains queryable via `GET /cashflow/categories/{id}/classification`'s `history` array.
- `SpendingFloor`, the attribution-driver tagging ("Controllable, not Structural"), and the FI-date
  recomputation US-013's second scenario describes are all out of scope — they require the Forecast
  and Attribution engines (M2+), which don't exist yet. This slice provides the one fact those
  engines will eventually need (a category's current classification, with full history), not the
  consumers of that fact.
- If a real proposal heuristic is designed later (recurring-pattern matching, a trained classifier,
  whatever it turns out to be), it's an additive endpoint — a `POST .../propose` returning a
  suggestion without persisting anything — not a change to `ClassifyCategoryHandler` or the storage
  shape decided here.
