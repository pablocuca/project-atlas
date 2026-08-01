# ADR-0006 — Immutable, content-addressed forecast artifacts

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Principal Architect

## Context

Two of Atlas's core capabilities are impossible without this decision:

1. **"What changed since yesterday?"** — requires distinguishing *your life changed* from *we
   shipped a new model*. If forecasts are recomputed on read with current code, that distinction is
   permanently lost.
2. **Calibration** — requires scoring a forecast *as it was stated*, years after it was made. A
   forecast that cannot be retrieved exactly as issued cannot be graded.

Recomputing on read is the obvious, cheap, and wrong design.

## Decision

Every forecast is an **immutable artifact**, content-addressed by
`(TwinSnapshot hash, ModelVersion, AssumptionSet hash, Seed)`, stored permanently in blob storage
with a PostgreSQL index. Artifacts are never mutated and have **no deletion path**. Re-running an
identical key must reproduce a bit-identical artifact.

## Rationale

- Content addressing means identical inputs cannot produce two stored results — the identity *is*
  the computation.
- Immutability makes historical comparison well-defined, which is the precondition for honest
  attribution (BR-203).
- Permanent retention is affordable: ~10–40 KB compressed per artifact, well under 1 GB across
  40 years of daily forecasts. Cost can never be a reason to delete.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Recompute on demand | No storage; always current code | Historical comparison and calibration both become impossible | Destroys two core capabilities |
| Store only the latest forecast | Minimal storage | No history, no calibration, no trend | Same |
| Store results, mutable | Simple updates | "Updating" a forecast is meaningless — it is a statement made at a time | Category error |
| Time-series database of outputs | Efficient trends | Loses the full distribution, assumptions, and reproducibility metadata | Insufficient fidelity for scoring |

## Consequences

### Positive
- Any number the user has ever seen is reproducible and explicable, forever.
- Calibration becomes possible, which is the product's trust mechanism.
- Model upgrades are safe: old artifacts remain valid statements under their own model version.

### Negative — accepted costs
- Storage grows forever. Quantified and accepted as negligible.
- Determinism becomes a hard engineering requirement — explicit seeds, no ambient clock, no
  parallel non-determinism, canonical serialisation. This is real work, enforced by a CI gate.
- Artifact schema readers must be retained permanently (BR-A03).

## Reversal cost

**Very high.** Calibration history and comparability would be lost irrecoverably. Effectively
irreversible, and intentionally so.

## Compliance

INV-120..INV-123, BR-300..BR-303. CI determinism gate replays a fixed corpus and asserts
bit-identity. Historical-corpus test deserialises every schema version ever shipped.

## References
[Forecast Engine](../../04-engines/02-forecast-engine.md) · [Calibration & Scoring](../../04-engines/06-calibration-and-scoring.md) · [Data Strategy §3](../04-data-strategy.md)
