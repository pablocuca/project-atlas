# Decision 0002 — Append-only schema, no `decision_to`

**Status:** Accepted · **Date:** 2026-08-02 · **Extends:** [ADR-0002](../03-architecture/adr/ADR-0002-bitemporal-double-entry-ledger.md) · **Corrects:** [Data Strategy §2.2–2.3](../03-architecture/04-data-strategy.md)

## Context

`docs/03-architecture/04-data-strategy.md` §2.2 sketched `ledger.journal_entry` with a
`decision_to timestamptz` column, described as implementing "logical retraction of a belief": when
an entry is corrected, the original row's `decision_to` is updated to close its belief interval.

That design requires an `UPDATE` statement against a truth-table row. It directly contradicts three
other places in the same specification, all at the same Architecture layer:

- **ADR-0002**, this schema's own governing decision: "An entry, once posted, is immutable...
  Database role has no `UPDATE`/`DELETE` on truth tables."
- **INV-031 / BR-101**: "Posted entries are immutable. Corrections are reversal + replacement."
- **`docs/03-architecture/06-security-strategy.md`**, STRIDE tampering mitigation: "Append-only
  storage; DB role has no `UPDATE`/`DELETE` on truth tables; **corrections are new rows**."

`Atlas.Modules.Ledger.Domain.JournalEntry.Correct(...)` (M0 Slice 1) already implements the
new-rows-only model — it returns two brand-new `JournalEntry` instances and never mutates the
original — and is tested (`Correct_produces_two_new_entries_and_never_mutates_the_original`,
`Modules.Ledger.Domain.Tests/JournalEntryTests.cs`).

## Decision

The `decision_to` column is removed. `ledger.journal_entry` is truly append-only: `INSERT` is the
only DML statement the `atlas_ledger` role can execute against it or `ledger.posting`, ever — no
`UPDATE` grant exists, at the database permission level, not just by convention.

A `kind` column (`'Original' | 'Reversal' | 'Replacement'`) is added instead, since without it a
Reversal and a Replacement — both just rows with `corrects_entry` set to the same original — would
be indistinguishable. `JournalEntry.Kind` already exists in Domain; this makes it round-trip.

The as-of query drops the `decision_to > $D` interval-containment clause entirely:

```sql
SELECT commodity, SUM(minor_units)
FROM ledger.posting p JOIN ledger.journal_entry e USING (entry_id)
WHERE e.tenant_id = $tenant AND p.account_id = $account
  AND e.valid_time <= $V AND e.decision_time <= $D
GROUP BY commodity;
```

A reversal's negated postings (BR-100: each commodity balances to zero on its own, so a reversal's
postings are the original's with direction flipped) net out algebraically once `decision_time <= $D`
brings them into scope — no interval bookkeeping needed. This is simpler than the original design,
not just safer.

## Consequences

- `docs/03-architecture/04-data-strategy.md` §2.2–2.3 is corrected in the same PR as this decision,
  per the Definition of Done ("the governing document is updated in the same PR, not a follow-up").
- `NFR-705` ("No deletion path exists for ledger, snapshots, or artifacts | 100% | Type + DB
  permission") extends naturally to "no *update* path either" — both are now literally true of the
  `atlas_ledger` role's grants, not just the application code's behaviour.
- Any future schema change to `ledger.journal_entry` or `ledger.posting` must remain additive-only
  (`docs/03-architecture/04-data-strategy.md` §4) — this decision does not relax that; it removes a
  column that should not have implied mutation in the first place.
