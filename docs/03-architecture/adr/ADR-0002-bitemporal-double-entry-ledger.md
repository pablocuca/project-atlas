# ADR-0002 — Bitemporal double-entry ledger as system of record

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Domain

## Context

Atlas's central promise is answering *"what changed since yesterday, and why?"*. That question is
**structurally unanswerable** under conventional storage, for one reason that most personal finance
software never confronts:

> A broker corrects a trade from three weeks ago. Did the world change, or did our knowledge change?

Single-timeline storage cannot distinguish these. When it cannot, every delta the product displays
silently conflates *your life changed* with *we were wrong before* — which destroys the credibility
of the entire attribution layer, and therefore of the product.

Separately, without double-entry, "what changed" must be computed ad hoc per metric, and there is
no structural check that the numbers are consistent.

## Decision

We will store financial truth in an **append-only, bitemporal, double-entry ledger**. Every fact
carries `ValidTime` (when it was true in the world) and `DecisionTime` (when Atlas learned it).
Entries are immutable; corrections are reversal-plus-replacement. Every entry balances to zero per
commodity. All balance queries require both time coordinates.

## Rationale

- Bitemporality is the only structure that answers all three questions: what is true now, what we
  believed then, and what was actually true then.
- Double-entry makes "what changed" **derivable** rather than computed, and gives a structural
  invariant (`Σ = 0`) that catches whole classes of error at write time.
- Append-only makes the ledger auditable and makes every projection rebuildable (AI-1).
- The technique is 500 years old for double-entry and 30 years old for bitemporality. Neither is
  novel; both are simply usually skipped.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Single-entry transaction table | Simple; what most apps do | No structural consistency check; correction handling ad hoc; deltas untrustworthy | Fails the core product promise |
| Double-entry, uni-temporal | Consistent; simpler queries | Cannot distinguish late-arriving data from real change | Fails the core product promise |
| Full event sourcing with no ledger semantics | Complete history | Reconstructing balances requires bespoke logic; no `Σ=0` invariant | Ledger semantics are strictly better here |
| Audit-log alongside a mutable table | Familiar | Two sources of truth that drift; "as of" queries become archaeology | The drift is guaranteed, and silent |

## Consequences

### Positive
- Every historical statement Atlas ever made is reconstructible and defensible.
- Projections are disposable, making read-model schema migration a non-event.
- Reconciliation becomes an invariant (INV-040), not a periodic report.

### Negative — accepted costs
- Every query is more complex; the API deliberately offers no single-time convenience overload.
- Storage grows monotonically. Quantified as trivial: ~10⁶ rows over 40 years.
- Developers unfamiliar with bitemporality face a real learning curve. Mitigated by keeping all
  time handling inside the Ledger module behind a narrow port.

## Reversal cost

**Very high — effectively irreversible.** This is the deepest structural commitment in the system.
It is accepted precisely because it is load-bearing for the product's central claim.

## Compliance

INV-002, INV-030..INV-035, BR-100..BR-109. Database role has no `UPDATE`/`DELETE` on truth tables.
Deferred trigger enforces per-commodity balance. Golden replay test (BR-107) in CI.

## References
[Data Strategy §2](../04-data-strategy.md) · [Domain Model §2](../../02-domain/04-domain-model.md)
