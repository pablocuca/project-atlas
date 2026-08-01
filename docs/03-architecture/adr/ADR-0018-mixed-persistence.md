# ADR-0018 — Event sourcing where the log is the truth; state persistence elsewhere

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** Principal Architect

## Context

Event sourcing is a natural fit for parts of Atlas and pure ceremony for others. Applying it
uniformly — in either direction — would be a consistency preference overriding a correctness
judgement.

A `JournalEntry` **is** an event; storing it as one is free and removes an impedance mismatch. A
`SpendingModel` is a *fitted statistical object* — event-sourcing its parameter updates adds
machinery and answers no question anyone will ask.

## Decision

Use **event sourcing where the event log is the domain truth**: Ledger, Goals & Policy, Incidents.
Use **conventional state persistence with versioned history** elsewhere: Cashflow, Liabilities,
Human Capital, Positions (a projection), Progression.

This mixed model is deliberate and documented, not an inconsistency.

## Rationale

- Event sourcing pays for itself only when the history of *how state got here* is itself a domain
  question. For the ledger, policy decisions, and incidents, it is the primary question.
- For fitted models, what matters is *which version was used for which forecast* — solved by
  version pinning in the `TwinSnapshot`, not by an event log.
- Positions are a projection over the ledger and are fully rebuildable (AI-1), so they need no
  independent history at all.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Event source everything | Uniform; complete history | Ceremony and complexity where no question requires it; projection rebuild cost everywhere | Consistency for its own sake |
| Event source nothing | Simplest | Loses the ledger's natural form and policy history; bitemporality becomes bolted-on | Ledger truth demands it |
| CQRS with separate stores | Independent scaling | Two datastores, eventual consistency, and cost — all unjustified at this volume | Over-engineered |

## Consequences

### Positive
- Each context uses the model that fits its actual questions.
- Lower complexity where event sourcing would add none.
- Projections stay disposable and rebuildable.

### Negative — accepted costs
- Developers must know which style applies where. Mitigated by documenting it in the module
  catalogue ([Modular Monolith §3](../03-modular-monolith.md)) and by module-local persistence.

## Reversal cost

**Low, per module.** Persistence style is a module-internal decision behind a port; no other module
can observe it.

## Compliance

Module catalogue records the persistence style per module. Architecture tests verify that no module
exposes its persistence style through its contracts.

## References
[Modular Monolith §3](../03-modular-monolith.md) · [Architecture Vision §5](../01-architecture-vision.md)
