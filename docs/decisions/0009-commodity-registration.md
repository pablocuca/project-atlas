# Decision 0009 — Commodity gains a runtime Register seam

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Decision 0005](0005-kernel-additions-m0.md), [Decision 0010](0010-positions-lot-tracking-and-custo-medio.md)

## Context

`Atlas.Kernel.Commodity.BySymbol` only ever knew `BRL` and `USD`, by design — Decision 0005 recorded
this limitation explicitly and named its own trigger: *"If Kernel ever needs a commodity lookup
beyond BRL/USD before a real master-data table exists, that's the trigger to build one properly
rather than extending the static dictionary further."* Positions (C8, FR-201/FR-202) is that
trigger: it needs Ledger to post and re-hydrate individual equity tickers now, in M1, well before
MarketData's real master-data table (FR-205+, M2) exists. `JournalEntryRepository`'s own row-reading
code calls `Commodity.BySymbol` to reconstruct a stored posting — so this isn't just a Positions-side
convenience; without a way to introduce a new symbol, Ledger itself cannot even post or read back an
entry naming an instrument it doesn't statically know.

## Decision

Build the "one properly" 0005 asked for at the scope M1 can actually support: `Commodity.BySymbol`'s
backing dictionary becomes a `ConcurrentDictionary`, and a new `Commodity.Register(Commodity)` adds
to it at runtime — a real registration API, not another `public static readonly` entry appended
next to `Brl`/`Usd`. Additive-only, idempotent for an identical redefinition, and throws if a symbol
is re-registered with different fields (protecting against two callers silently disagreeing about
what a symbol means). `BRL`/`USD` are seeded in as before, so every existing call site is
unaffected. `PositionsEndpoints.RegisterInstrument` (`POST /positions/instruments`) is the concrete
seam a caller uses to introduce a new tradable instrument before posting the first trade referencing
it.

## Consequences

- This is a Kernel addition mid-milestone, the same category of change ADR-0005 already established
  a retroactive-documentation precedent for during M0.
- Still not a commodity master-data table: no persistence, no jurisdiction/regulatory metadata
  beyond what `Commodity.Create`'s existing fields carry, no listing/delisting lifecycle. Registered
  commodities live only in the running process's memory — restarting `Atlas.Host` forgets them,
  which is fine today (every registration is a fast, explicit, idempotent HTTP call an integrator
  makes before posting a trade) but is real debt if a real trading integration arrives before
  MarketData's actual table does. Tracked in the debt register.
- The failure mode for a genuine symbol collision (two different instruments contending for one
  ticker, or the same ticker re-registered with a different `MinorUnitScale`) is a thrown exception,
  not a silent overwrite — deliberately loud, since a silent redefinition would corrupt every
  already-posted entry's meaning.
