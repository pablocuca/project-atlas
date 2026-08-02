# Decision 0005 — Kernel additions made during M0 (retroactive)

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Risk Register RISK-020](../06-governance/01-risk-register.md)

## Context

`docs/06-governance/01-risk-register.md` (RISK-020, "Kernel grows into a shared-utils dump") names
its trigger as "any kernel addition without an ADR." Three additions landed in `Atlas.Kernel` across
M0 Slices 1–2 without one: `Unit` (Slice 1), `Commodity.BySymbol` (Slice 2), and `ToString()`
overrides on `AccountId`/`EntryId` to match `TenantId`'s existing one (Slice 3, in
`Atlas.Modules.Ledger.Domain`, not Kernel — noted here only because it was reasoned about at the
same time). This is that ADR, written after the fact rather than not at all, because the M0
wrap-up review is what caught the gap.

## What was added, and why each one belongs in Kernel

- **`Unit`** (`src/Atlas.Kernel/Unit.cs`) — a "no value" type for a `Result<T>` that only signals
  success or failure. Needed the moment `JournalEntry`'s internal `ValidatePostings` wanted to
  return `Result<Unit>` rather than reach for an exception on a validation-only path. It belongs in
  Kernel because `Result<T>` itself is already there, and a `Result<Unit>` user shouldn't need a
  second package for a type this small.
- **`Commodity.BySymbol`** (`src/Atlas.Kernel/Commodity.cs`) — resolves a `Commodity` back from its
  stored symbol string. Needed because `docs/03-architecture/04-data-strategy.md` §2.2 stores
  `posting.commodity` as bare `text`, deliberately, with no commodity master-data table (none exists
  until MarketData, M1/M2) — `Ledger.Infrastructure`'s read path has nothing else to resolve a
  `Commodity` from. It belongs in Kernel because `Commodity` itself is already there, and every
  layer that persists or transmits a commodity symbol needs the same round-trip.

## Why this doesn't (yet) trigger RISK-020's concern

Both additions extend types Kernel already owns (`Result<T>`, `Commodity`) rather than introducing
new concepts or external dependencies — `Unit` and `Commodity.BySymbol` add zero `PackageReference`s
and zero `ProjectReference`s. RISK-020's actual fear is Kernel becoming a dumping ground for
unrelated utility code; two small, load-bearing extensions of existing Kernel types is a different
thing from that. `Commodity.BySymbol` does carry a real limitation worth stating: it only resolves
`Commodity.Brl` and `Commodity.Usd` because those are the only two commodities Kernel knows about
today. It will need to become a real lookup once a commodity master-data table exists — that's
already noted at the call site, not a hidden gap.

## Consequences

- The next Kernel addition should come with its own ADR *at the same time*, not accumulate for a
  milestone-boundary review to catch — this decision exists specifically so that doesn't become the
  pattern.
- If Kernel ever needs a commodity lookup beyond `BRL`/`USD` before a real master-data table exists,
  that's the trigger to build one properly rather than extending the static dictionary further.
