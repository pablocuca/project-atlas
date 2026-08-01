# Decision 0001 — Correction mechanics

**Status:** Accepted · **Date:** 2026-08-01 · **Extends:** [ADR-0002](../03-architecture/adr/ADR-0002-bitemporal-double-entry-ledger.md)

## Context

ADR-0002 establishes that corrections are "reversal-plus-replacement," and `INV-031`/`BR-101` say a
correction is "a reversal plus a replacement, both linked to the original." Neither document, nor
the domain model (`docs/02-domain/04-domain-model.md`), specifies the concrete mechanics: how many
new entries are created, whether the original's `status` field is physically mutated, what
`ValidTime` the new entries carry, or how `Posting.Money` represents sign. Implementing
`JournalEntry.Correct(...)` requires all four to be pinned down. This decision resolves them.

## Decision

1. **A correction creates exactly two new `JournalEntry` records** — a *Reversal* (postings are the
   exact negation of the original's) and a *Replacement* (the corrected postings). Both set
   `CorrectsEntryId = original.Id`. The field is a provenance pointer, not a unique foreign key —
   two entries are allowed to reference the same original.

2. **The original entry is never mutated, ever — including `status`.** `status = Reversed` is a
   *derived* read (`∃ entry : entry.CorrectsEntryId = this.Id ∧ entry.Kind = Reversal`), never a
   stored column that gets updated. This is what makes ADR-0002's compliance note — "database role
   has no `UPDATE`/`DELETE` on truth tables" — literally true rather than a convention someone can
   violate by mistake.

3. **`ValidTime` of the reversal and the replacement equal the original entry's `ValidTime`.** A
   correction restates what was true at the original event date; only `DecisionTime` advances. This
   makes bitemporal replay do all the work with no branching: querying
   `asOf(originalValidTime, beforeCorrectionDecisionTime)` replays to the original (wrong) belief;
   querying `asOf(originalValidTime, now)` replays original + reversal + replacement down to the
   corrected truth. Both remain queryable forever, which is the concrete mechanism behind the M0
   exit-gate requirement that "a correction to a 3-week-old entry preserves both the original belief
   and the corrected truth."

4. **`Posting.Money.Amount` is always non-negative; `Direction` alone carries the sign.** This
   matches `Money`'s largest-remainder split (`INV-011`), which assumes unsigned magnitudes, and
   avoids a redundant, potentially-inconsistent second sign representation.

## Consequences

- `LedgerReplay` needs no special-casing for corrections: it is a fold over `JournalEntry` ordered by
  `DecisionTime`, and a Reversal's negated postings cancel the original's postings algebraically.
- A `Reversed` entry can itself be corrected again (a Reversal or Replacement is an ordinary
  `JournalEntry` and can be the target of a future `CorrectsEntryId`) — this decision does not forbid
  re-correction, and no rule in the specification requires forbidding it.
- `Correct(...)` requires the original entry's postings to compute the negation; it cannot be called
  with only an id.
