# ADR-0010 — Manual-first ingestion, adapters as anti-corruption layers

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Product

## Context

Open Finance Brasil does not grant individuals direct API access — participation is regulated and
requires being an authorised institution. The practical routes are commercial aggregators (Pluggy,
Belvo), which cost money (plausibly more than the entire infrastructure budget), require contracts,
and can change terms unilaterally.

Personal finance projects overwhelmingly die at this step: the developer builds the integration
first, hits a wall, and the project stalls before any domain value exists.

## Decision

**Manual entry is Source #1**, subject to identical invariants, reconciliation, and idempotency as
any automated feed. CSV/OFX import is M1. Aggregator integration is **M5 or later** and is
explicitly an *efficiency* feature, never a *capability* feature. Every source implements
`ISourceAdapter` as an anti-corruption layer; no external schema ever reaches the Ledger.

Raw payloads are archived to blob **before parsing**, forever.

## Rationale

- The critical path to product value must not contain a third-party commercial dependency.
- Building the ledger, tax engine, and forecast against hand-entered data forces domain correctness
  before volume can hide errors.
- Graceful degradation forever: any source can break, and the system degrades to *slower*, never to
  *broken*.
- Archiving raw payloads makes parser bugs **retroactively fixable** — a 2031 fix can be replayed
  against 2026 data. This costs a few MB per year and converts a permanent data loss into a
  recoverable inconvenience.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Aggregator-first | Rich data quickly; low user effort | Blocks all value on a contract; cost may exceed the whole budget; single point of failure | Puts a commercial dependency on the critical path |
| Screen scraping | No contract needed | Fragile, likely violates ToS, credential handling risk | Unacceptable security and legal posture |
| Manual only, forever | Simple; total control | High ongoing effort; categorisation coverage suffers over years | Automation is genuinely valuable once the domain is right |
| Parse only, no raw archive | Less storage | Parser bugs become permanent data loss | Trivially cheap to avoid |

## Consequences

### Positive
- The project can reach full functional value with zero external dependencies.
- Correctness is proven on small, well-understood data before scale.
- Adapters are independently versioned, tested against golden corpora, and replaceable.

### Negative — accepted costs
- Real user effort in the early milestones. Mitigated by quick-capture UX and CSV import at M1.
- Data coverage will be lower initially, which the Coverage SLI will honestly report as degraded.

## Reversal cost

**None — this is additive.** Adding an aggregator later requires only a new `ISourceAdapter`.

## Compliance

BR-103, BR-109, and the adapter rules in [Ingestion §5](../05-ingestion-and-integration.md).
Golden-file corpus per adapter runs in CI to detect layout drift before it produces wrong numbers.

## References
[Ingestion & Integration](../05-ingestion-and-integration.md) · [Risk Register RISK-006](../../06-governance/01-risk-register.md)
