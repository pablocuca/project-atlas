# ADR-0003 — Integer minor-unit money, no floating point

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Domain

## Context

Atlas compounds monetary values over 40-year horizons across millions of Monte Carlo paths, and
computes Brazilian tax with statutory rounding rules. Floating-point error is not merely inelegant
here — accumulated over that many operations it becomes materially visible, and it makes
deterministic replay (AI-2) impossible across runtimes and hardware.

## Decision

`Money` is `(int64 amount in minor units, Commodity)`. Floating-point types are **banned from every
financial code path** by analyser. Division returns quotient and remainder; remainders are allocated
by the largest-remainder method so split amounts always re-sum. Rounding is half-even, applied once,
at a declared boundary.

## Rationale

- `int64` centavos represents ±R$ 92 quadrillion — vastly beyond any conceivable need.
- Integer arithmetic is exact, deterministic, and identical on every platform. This is a
  prerequisite for the determinism gate, not merely a nicety.
- Explicit remainder handling makes "the cents that vanished" impossible rather than unlikely.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| `decimal` (.NET 128-bit) | Exact for decimal fractions; familiar | 4× memory; slower in hot simulation loops; still allows unit-less arithmetic | Type safety matters more than familiarity; integer is faster where it counts |
| `double` | Fast; simple | Inexact; non-deterministic across platforms; catastrophic for compounding and replay | Disqualifying |
| Rational / arbitrary precision | Exact for all operations | Slow; unbounded growth in Monte Carlo loops | Cost with no benefit at this precision requirement |

## Consequences

### Positive
- Deterministic replay is achievable at all (AI-2 depends on this).
- Cross-commodity arithmetic is a compile error, not a subtle bug.
- Simulation inner loops are fast and allocation-free.

### Negative — accepted costs
- Percentage and rate maths requires explicit scaling discipline at boundaries.
- Sub-centavo intermediate precision (e.g. unit prices, fund quotas) needs a separate
  higher-precision type for quantities. Quantities use `decimal`; **money never does**.

## Reversal cost

**High.** `Money` is in `Atlas.Kernel` and used everywhere. Changing it is a mechanical but
project-wide refactor.

## Compliance

INV-001, INV-005, INV-010..INV-012, BR-001..BR-005. Roslyn analyser bans `double`/`float` in
`*.Domain` and any type named `*Money*`. Property test asserts split-and-resum for random amounts
and divisors.

## References
[Domain Model §1](../../02-domain/04-domain-model.md) · [Coding Standards](../../05-engineering/02-coding-standards.md)
