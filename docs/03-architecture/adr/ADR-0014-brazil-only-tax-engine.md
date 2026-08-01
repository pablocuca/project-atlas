# ADR-0014 — Brazil-only tax engine behind `ITaxJurisdiction`

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Domain

## Context

Tax drag and withdrawal sequencing dominate the Brazilian FI date — plausibly by years. Getting
Brazilian tax right is therefore the deepest source of correctness and the strongest moat available
to this product. Supporting a second jurisdiction would roughly double the effort in the single
hardest context while halving the depth achieved in each.

There is also a well-known trap in the opposite direction: building a generic "tax DSL" with no
correct concrete implementation — an abstraction with no instance, which is guaranteed to fit no
jurisdiction properly.

## Decision

Implement **Brazil only, in depth**, behind an `ITaxJurisdiction` interface with exactly one
implementation. The interface exists to keep the seam honest and the module pure — **not** to
enable a second jurisdiction soon.

## Rationale

- Depth in one jurisdiction beats breadth across two, when the jurisdiction is where the moat is.
- One interface with one implementation is cheap and keeps the dependency direction correct
  (Taxation depends on nothing).
- The interface is shaped by a *real* implementation, so if a second jurisdiction ever arrives, the
  abstraction will be grounded in reality rather than speculation.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Brazil + Portugal/EU now | Broader appeal; multi-currency early | Doubles the hardest context; forces FX and purchasing-power modelling into the core immediately; halves depth | Depth is the moat |
| Generic tax DSL, no deep implementation | Maximum flexibility | Abstraction before instance; fits nothing properly; unfalsifiable | The classic premature-abstraction trap |
| Hard-code Brazil with no interface | Simplest | Tax logic leaks into other modules; purity and replayability compromised | The interface costs almost nothing and protects a lot |

## Consequences

### Positive
- Maximum depth: come-cotas, tabela regressiva, exemptions, PGBL/VGBL, withdrawal sequencing.
- Taxation stays a pure, stateless, deterministic function library — the precondition for replay.
- A second jurisdiction is additive whenever it is genuinely wanted.

### Negative — accepted costs
- Non-Brazilian users cannot use Atlas meaningfully, which limits the open-source audience.
- Brazilian tax law changes require ongoing maintenance. Handled by versioned rulesets (ADR-0017)
  and an annual calendar-triggered review.

## Reversal cost

**Low — additive.** A second jurisdiction is a new `ITaxJurisdiction` implementation plus
multi-currency work in Valuation.

## Compliance

MR-7 (Taxation references nothing), INV-052, BR-400..BR-408.

## References
[Tax Engine — Brazil](../../04-engines/07-tax-engine-brazil.md) · [ADR-0017](ADR-0017-versioned-tax-rulesets.md)
