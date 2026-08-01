# ADR-0001 — Modular monolith over microservices

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Principal Architect

## Context

Atlas is designed to run for decades, and the founding brief says "modular monolith first,
microservices later". That instinct is right, but the *reason* usually given for it — "start
simple, split when you scale" — is wrong for this system and would lead to the wrong seams.

The real facts: one user, ~10⁵–10⁶ ledger rows over 40 years, no horizontal scaling pressure that
will ever arrive, a US$30/month cost ceiling, and a single maintainer. The genuine long-term risk
is **not** scaling. It is **invisible coupling accumulating over ten years** until change becomes
impossible.

## Decision

We will build a **modular monolith** in which one bounded context maps to one module, module
boundaries are enforced at compile time by three independent mechanisms, and each module owns its
own PostgreSQL schema with database-level access control. Cross-module communication is exclusively
by published contract or domain event.

## Rationale

- Microservices make coupling *expensive*, not *visible*. Compile-time module boundaries make it
  **visible**, which is what actually protects a decade-long codebase.
- At N=1, distributed systems buy nothing and cost operational complexity, latency, debugging
  difficulty, and money — every day, forever.
- In-process refactoring remains available. Extraction remains available because the seams already
  exist (events, contracts, schema isolation).
- The one component with a genuinely different resource profile — Monte Carlo simulation — is
  already isolated as a separate Container Apps Job, so the one real case is already solved.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Microservices from day one | Hard boundaries; independent deploy | 10× ops cost; distributed debugging; violates the cost ceiling; no benefit at N=1 | Solves a problem that will never occur, at permanent cost |
| Unstructured monolith | Fastest to start | Coupling becomes invisible and irreversible; the exact decade-scale failure mode | Directly contradicts the project's purpose |
| Serverless functions per capability | Scale-to-zero granularity | Shared-nothing forces a distributed data layer; cold starts everywhere; orchestration complexity | Worse cold-start and worse coupling story |
| Modular monolith **without** enforcement | Cheap; feels the same on day one | Every unenforced modular monolith becomes a ball of mud. This is empirically near-universal | Enforcement is the entire value |

## Consequences

### Positive
- One deployable, one debugger, one transaction boundary, one log stream.
- Refactoring across module boundaries is a compile error, not a production incident.
- Extraction to services later is a transport swap behind `IEventBus`, not a rewrite.

### Negative — accepted costs
- A runaway module can consume the whole process's resources. Mitigated by isolating simulation.
- Deployment is all-or-nothing. Acceptable: deploys take minutes and rollback is one command.
- Discipline is required indefinitely. This is why enforcement is triple-layered.

### Follow-on
- `Atlas.ArchitectureTests` implementing MR-1..MR-10.
- Per-module Postgres roles provisioned in Bicep.
- Roslyn analysers for ambient clock, floating-point money, and cross-layer access.

## Reversal cost

**Low to moderate, by design.** Every module already communicates via contracts and events, and
owns its schema. Extracting one means implementing a `ServiceBusEventBus`, moving a schema, and
deploying a second container. Revisit when a module meets ≥2 of the five extraction criteria in
[Modular Monolith §8](../03-modular-monolith.md).

## Compliance

`Atlas.ArchitectureTests` (MR-1..MR-10), `Directory.Build.props` reference guards, Roslyn analysers,
per-module database roles. All four run in CI on every PR.

## References
[Architecture Vision](../01-architecture-vision.md) · [Modular Monolith](../03-modular-monolith.md) · [Bounded Contexts](../../02-domain/02-bounded-contexts.md)
