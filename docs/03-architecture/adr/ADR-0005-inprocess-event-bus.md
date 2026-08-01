# ADR-0005 — In-process event bus with transactional outbox

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** Principal Architect

## Context

Atlas is event-driven internally: the Truth Loop feeds the Foresight Loop feeds the Decision Loop.
The founding brief names Azure Service Bus. At N=1, Service Bus costs ~US$10/month (a third of the
entire ceiling) and buys durability guarantees that a transactional outbox in the same database
provides for free.

The real requirement is not a message broker. It is: **state change and event publication must never
diverge.**

## Decision

We will publish domain events through an `IEventBus` abstraction backed by an **in-process
dispatcher fed by a transactional outbox** in `shared.outbox`. Publication enrols in the same
database transaction as the state change. A background dispatcher delivers at-least-once to typed,
idempotent handlers.

## Rationale

- The outbox pattern makes "state committed but event lost" **impossible without a distributed
  transaction** — a stronger guarantee than naive direct publication to a broker.
- `IEventBus` is the seam. Swapping to Service Bus is one adapter and one registration; the outbox
  is retained regardless, because it is what makes the swap safe.
- At-least-once with idempotent handlers is the correct semantic anyway. Building for it now means
  a future broker changes nothing about handler design.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Azure Service Bus now | Durable; decoupled; matches the brief | ~US$10/mo; network hop; another failure mode; zero benefit at N=1 | Cost with no current benefit; the seam preserves the option |
| Direct in-process dispatch, no outbox | Simplest | Event loss on crash between commit and publish. Silent, and corrupts downstream state | The exact failure the outbox exists to prevent |
| MediatR in-process only | Familiar; low ceremony | Same loss window as above; no retry or dead-letter | Insufficient durability |
| Postgres `LISTEN/NOTIFY` | Native; no polling | Not durable — notifications are lost if no listener is connected | Insufficient durability |

## Consequences

### Positive
- Zero marginal cost; one fewer external dependency.
- Strong delivery guarantee from day one.
- Handlers are written idempotent from the start, so the broker swap is genuinely transparent.

### Negative — accepted costs
- Polling the outbox adds a small, constant database load. Tuned to a 1-second interval.
- All handlers run in the API process, so a slow handler affects the process. Mitigated by keeping
  heavy work (simulation) out-of-process.
- Ordering is per-aggregate, not global. Handlers must not assume global ordering — documented and
  tested.

## Reversal cost

**Very low.** One adapter class, one DI registration, one Bicep resource. This ADR exists mainly to
record that the low reversal cost was *designed in*, not lucky.

## Compliance

`IEventBus` is the only publication path (analyser-enforced). Dead-letter count is a zero-budget
SLO. Handler idempotency is verified by a test that replays each event twice and asserts identical
state.

## References
[Modular Monolith §6](../03-modular-monolith.md) · [Architecture Vision §5](../01-architecture-vision.md)
