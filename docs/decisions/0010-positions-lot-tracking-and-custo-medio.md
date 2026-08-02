# Decision 0010 — Positions: lot tracking and custo médio as a pull-based Ledger projection

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [C8](../02-domain/02-bounded-contexts.md),
[R03](../02-domain/03-context-map.md), [ADR-0018](../03-architecture/adr/ADR-0018-mixed-persistence.md),
[INV-040–043](../02-domain/04-domain-model.md), [Decision 0009](0009-commodity-registration.md)

## Context

FR-201/FR-202 ("track lots with quantity, unit cost, acquisition date, instrument class"; "compute
cost basis using *custo médio* for Brazilian equities") specify *what* Positions must do, and
`docs/02-domain/04-domain-model.md` §3 specifies the target shape (`Position`, `Lot`, `Disposal`,
INV-040–045). Several implementation-level questions are left open, the same category of gap
Decisions 0001–0008 have each resolved for their own slice:

1. **How does Positions learn about a trade?** R03 names `JournalEntryPosted` as the published event
   for Ledger → Positions, but no real event bus exists yet (`IEventBusBuilder` has no dispatcher —
   there has never been a second module to need one). `Posting` also has no `lotRef` field, despite
   the domain model sketching one — no consumer existed yet to validate that shape against.
2. **Can a trade even be posted as a plain two-leg entry?** `JournalEntry.ValidatePostings` (BR-100)
   requires every commodity's postings to net to zero *independently* (INV-030: "each commodity
   balances independently via a trading/exchange account"). A naive "debit the equity account,
   credit the cash account" entry leaves both commodities non-zero and is rejected outright.
3. **Where does the custo médio computation live**, and where do `Disposal` records attach — the
   domain model nests `disposals: Disposal[]` under `Lot`, but custo médio (INV-043) settles a
   disposal against the *position's* running average, not any specific lot's cost.
4. **What is `Lot.instrumentClass`?** Never given its own value set in the spec.
5. **Is a full bitemporal query surface required this milestone?**

## Decisions

**Pull, not subscribe.** `SyncPositionHandler` rebuilds a `Position` on demand by calling
`IFindEntriesInRange` (Ledger.Contracts) — the same OHS port Ingestion's fuzzy duplicate detection
already proved — rather than subscribing to a `JournalEntryPosted` stream. This isn't a stand-in for
the "real" design: ADR-0018 already requires Positions to be "a projection over the ledger... fully
rebuildable," so pull-and-replace *is* the target shape, not a shortcut pending an event bus.

**Trade legs are identified by account, not by commodity uniqueness.** Because BR-100 forces a
clearing leg per commodity (finding #2 above), a real trade entry carries *two* postings in the
traded commodity (the position account and its clearing counterpart) and *two* in cash (the cash
account and its clearing counterpart) — four legs, not two. `SyncPositionHandler.HandleAsync` takes
`positionAccountId` and `cashAccountId` explicitly and picks
`entry.Postings.Single(p => p.AccountId == positionAccountId)` (and the same for cash) rather than
matching on commodity. This resolves the "no `Posting.lotRef`" gap without touching Ledger's already-
shipped contracts: the *account* is the link Positions needs, and it's already on every posting.

**Disposals are tracked on `Position`, not nested under a `Lot`.** INV-043 settles every disposal at
the position's current weighted-average unit cost, never a specific lot's own cost — there is no
lot for a disposal to "belong to" under custo médio. `Position.Disposals` is a flat audit trail;
`Position.Lots` is untouched by a disposal, exactly as INV-043 requires ("Lots remain individually
tracked for audit... the taxable basis for equities is the weighted average").

**`Lot.instrumentClass` is `Commodity.Kind`.** No separate enum — C8's own boundary statement
("owns cost basis mechanics; the Tax context owns tax treatment of that basis") means the
classification Positions needs is exactly the one `Commodity` already carries. `ClassifyLot`
(Taxation, `ITaxJurisdiction`) is free to map `CommodityKind` to a `TaxRegime` however Brazilian tax
law requires — that mapping is Taxation's, not Positions'.

**As-of support, not full bitemporal query.** `SyncPositionHandler` accepts `asOfValidTime` and
`asOfDecisionTime` and filters accordingly — INV-040 ("position quantities reconcile to ledger
quantities exactly") is proven at whatever coordinate the caller asks for, matching how Ledger's own
`BalanceAt` works. What's deferred is a *persisted* bitemporal position history: the `positions`
schema stores one row per tenant+commodity (current-as-of-last-sync), not a row per historical
sync. Re-running a sync at an earlier `asOfValidTime` recomputes correctly but overwrites the stored
row rather than keeping prior syncs around — acceptable because the projection is, by ADR-0018,
never the source of truth and is always re-derivable from Ledger on demand.

**M2 fields (`CorporateAction`, `Valuation`, `MarkSet`) are omitted entirely**, not stubbed. FR-201/
FR-202 don't need them, and an empty placeholder for a shape nobody has designed yet is worse than
no field.

## Consequences

- A trade must be posted as a 4-leg Ledger entry (position + its clearing leg, cash + its clearing
  leg). This is more ceremony than a naive 2-leg entry, but it's what BR-100 already requires for any
  cross-commodity economic event — Positions didn't introduce this, it's the first consumer to
  actually exercise it. `PositionSyncTests.PostTradeAsync` in the integration suite is the reference
  shape for how a real trade importer (a future OFX broker-statement adapter, FR-108-adjacent) would
  need to construct one.
- `SyncPositionHandler` throws (not `Result<T>`-fails) if an entry naming the position account
  doesn't have exactly the shape it expects (one leg on `positionAccountId`, one on `cashAccountId`).
  Per `docs/05-engineering/02-coding-standards.md` §2 (Result for expected failures, exceptions for
  bugs): a well-formed trade entry always has this shape, so an entry that doesn't is a genuine "this
  posting isn't a trade Positions understands" bug to surface loudly, not a case to guess at.
- If a real event-bus dispatcher is ever built for another reason, Positions doesn't need to change
  its persistence model to benefit from it — only `SyncPositionHandler`'s trigger (an HTTP call today)
  would move to "on every `JournalEntryPosted`." The rebuild-and-replace repository shape is
  unaffected either way.
- Registering a new tradable instrument (`POST /positions/instruments`, Decision 0009) is a separate,
  explicit, caller-driven act from posting the first trade referencing it — consistent with Decision
  0006's "running an import is itself a deliberate act" reasoning.
