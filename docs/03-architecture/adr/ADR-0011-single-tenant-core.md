# ADR-0011 — Single-tenant core with a tenancy seam

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO

## Context

Two goals in the brief are in genuine tension: build a personal Financial Mission Control for one
person, *and* produce a world-class open-source Financial Operating System. Multi-tenancy taxes
every query, migration, index, test, and security review — paid for years before a second user
might exist. But retrofitting tenancy into a mature data layer is one of the most painful migrations
in software.

## Decision

Build **single-tenant** — no signup, no billing, no tenant management UI, no per-tenant operational
tooling — while threading `TenantId` through **every aggregate root, every table, and every query
from the first commit**. Tenancy is a seam that is built but not activated.

## Rationale

- The expensive, irreversible part of multi-tenancy is the **data model**, and that costs almost
  nothing to include now: one column, one index prefix, one query predicate.
- The expensive-to-operate parts — isolation guarantees, per-tenant keys, onboarding, support,
  billing, noisy-neighbour handling — are all deferrable without any structural debt.
- This converts "open source it later" from a rewrite into a project of known, bounded scope.
- It also improves the single-tenant system: `TenantId` in every query is a defence-in-depth
  boundary even with one tenant.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Full multi-tenancy now | Ready for any future | Slows domain depth — the actual differentiator — for years; adds security surface with zero users | Pays a large cost for an option that may never be exercised |
| No tenancy concept at all | Simplest possible | Retrofitting `TenantId` into a mature bitemporal ledger is brutal | The one genuinely irreversible mistake available here |
| Database-per-tenant | Strong isolation | Migration and cost multiply per tenant; over-engineered before tenant two | Can still be adopted later; the seam does not preclude it |

## Consequences

### Positive
- Maximum effort goes to the tax engine, attribution, and calibration — the real moats.
- No premature security or operational surface.
- Open-sourcing remains a genuine, costed option rather than a fantasy.

### Negative — accepted costs
- `TenantId` appears throughout the code and adds mild ceremony with no present benefit.
- Field-level encryption keys are already tenant-scoped, which is slightly more machinery than one
  user needs — and is what makes cryptographic erasure (ADR-0020) work.

## Reversal cost

**Low — that is the entire point.** Activating multi-tenancy means adding identity provisioning,
per-tenant key management, and row-level security policies. No data migration.

## Compliance

INV-003. Base aggregate type requires `TenantId`. A persistence guard fails any query against a
tenant-scoped table without a tenant predicate. Row-level security policies are pre-written and
disabled.

## References
[Bounded Contexts C18](../../02-domain/02-bounded-contexts.md) · [Security Strategy](../06-security-strategy.md) · [Non-Goals NG-07](../../00-foundation/04-non-goals.md)
