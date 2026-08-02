# Decision 0004 — `atlas-seed` is an HTTP client, not a database or Domain caller

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Repository Structure](../05-engineering/01-repository-structure.md), [Contributing §1](../05-engineering/06-contributing.md)

## Context

`tools/atlas-seed` is named in the spec as "a synthetic financial life generator... deterministic
from a seed... every developer, every CI run, and every demo uses it," but no document says how it
talks to the system. Two shapes were available: call `Atlas.Modules.Ledger.Application`'s handlers
(or `Ledger.Infrastructure`'s repositories) directly in-process, or drive the running `Atlas.Host`
over HTTP like any other client.

## Decision

`atlas-seed` is a plain HTTP client of `Atlas.Host`'s five `/ledger/*` routes. It takes a
`ProjectReference` only on `Atlas.Kernel` (for the value types — `Money`, `Commodity`, `ValidTime`,
`DecisionTime` — it needs to compute the balances it expects to see) and none of the module
projects.

The documented local-dev sequence is the direct evidence for this:

```bash
docker compose up -d
dotnet run --project src/Atlas.Host
dotnet run --project tools/atlas-seed -- --years 10 --seed 42
```

That ordering only makes sense if the host is already accepting requests when the seed tool starts —
it's a separate process, run after the API is up, exactly the way a real client (or a future Flutter
app) will use it. Calling the Application layer directly would mean `atlas-seed` needs its own
composition (its own `NpgsqlDataSource`, migration awareness, DI wiring) duplicating what
`Atlas.Host` already does, for no benefit — and would make the tool a de facto sixth "consumer" of
`Ledger.Infrastructure` living outside `src/Modules/`, which the module-boundary rules
(`docs/03-architecture/03-modular-monolith.md` §2) were never written to account for.

## Consequences

- `atlas-seed` cannot violate MR-1..MR-10 by construction — it isn't part of the module system, it's
  an external caller of the same API surface any other client uses.
- Running `atlas-seed` end-to-end doubles as a real exercise of the HTTP API under load (~1,000+
  sequential requests), not just a database-population script — closer to how the system will
  actually be used than a direct-write shortcut would be.
- It inherits whatever `Atlas.Host` currently has no auth for it (Decision 0003) — this is fine for
  local dev and CI, and stops being fine the moment `Atlas.Host` gains real authentication, at which
  point `atlas-seed` needs credentials like any other client.
- Determinism and idempotency (same `--seed` twice) are proven the same way any client's retries
  would be proven: by hitting the real `BR-103` duplicate-key constraint, not by a special bypass.
