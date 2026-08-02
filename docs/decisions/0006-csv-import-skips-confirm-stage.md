# Decision 0006 — CSV import skips the CONFIRM stage's confidence gate

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Ingestion & Integration §6](../03-architecture/05-ingestion-and-integration.md), [ADR-0010](../03-architecture/adr/ADR-0010-manual-first-ingestion.md)

## Context

The ingestion pipeline's CONFIRM stage (`docs/03-architecture/05-ingestion-and-integration.md` §6)
specifies when a proposal may auto-confirm without a human: adapter confidence ≥ 0.95, the
transaction matches an established recurring pattern, value ≤ R$500, it isn't an investment
transaction, and it doesn't affect Essential/Discretionary classification. Everything else queues
for review.

None of that machinery exists yet: no confidence-scoring model, no recurring-pattern detection, no
review queue, no UI to present a queued proposal to a human. Building the full gate now means
guessing at thresholds and a review UX with nothing real to calibrate either against.

## Decision

M1 Slice 1's CSV import posts every row that parses and normalises successfully, directly, with no
confidence gate and no review queue. Running an import is itself a deliberate act by the person
running it — closer in spirit to manual entry being a first-class source with no privileged bypass
(BR-109, ADR-0010) than to an automated background feed. The person who ran `--years 10 --seed 42`
for `atlas-seed` is the same kind of actor as the person who points this tool at a real bank CSV:
someone who chose, right now, to bring this data in.

## Consequences

- Every posted-from-import entry is real, tested Ledger data — Slice 1 doesn't introduce a
  provisional or lower-trust category of entry. A misclassified or wrong row is fixed the same way
  any wrong entry is fixed: `JournalEntry.Correct` (Decision 0001), not a queue-item edit.
- The real CONFIRM stage — confidence scoring, the R$500 ceiling, recurring-pattern matching, a
  review queue — is deferred, not abandoned. Its trigger is a UI existing to present queued
  proposals to a human; building the gate before that exists would be un-testable guesswork.
- `docs/03-architecture/05-ingestion-and-integration.md` §6 is not amended by this decision — it
  describes the target design. This decision records what Slice 1 does *instead*, and why that's a
  legitimate, bounded simplification rather than a silent gap.
