# Decision 0007 — Fuzzy cross-source duplicate detection design

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Ingestion & Integration §4](../03-architecture/05-ingestion-and-integration.md), [Decision 0006](0006-csv-import-skips-confirm-stage.md)

## Context

FR-110 and the architecture doc specify the *what* (date ±2 days, amount exact, counterparty
similarity ≥0.85, queue for human resolution, never auto-merge) but not the *how*: no similarity
algorithm, no query shape for "recent entries to compare against," no persistence design, no
resolution-UI scope. Four real decisions were needed to implement this at all.

## Decisions

1. **Similarity is substring-containment first, normalised Levenshtein as the fallback — not raw
   edit distance alone.** Checked against the architecture doc's own worked example ("Joao" manual
   entry vs "JOAO S" bank feed, the same PIX transfer): raw normalised Levenshtein scores that pair
   at **0.667**, below the 0.85 threshold the same document sets — the doc's own example would fail
   its own gate. Bank statements characteristically *append* metadata to a core counterparty name
   ("JOAO S PIX RECEBIDO", transaction codes, city), which edit distance penalises heavily even
   though a human reads it as an obvious match. Treating containment (one normalised string fully
   inside the other) as similarity 1.0, falling back to edit distance otherwise, is what makes the
   doc's own example actually clear the gate — verified in
   `tests/Modules.Ingestion.Domain.Tests/StringSimilarityTests.cs`.
2. **A new Ledger.Contracts port, `IFindEntriesInRange`**, not a query Ingestion runs itself. A
   cross-source duplicate by definition includes entries Ingestion never posted (manual entries,
   ADR-0010's "Source #1") — Ingestion has no visibility into those without asking Ledger. Same
   evolution `IPostJournalEntry` followed in M1 Slice 1: a port gets added when a real consumer
   needs it, not speculatively.
3. **One range query per import file, covering the whole file's date span (±2 days), not one query
   per row.** The comparison set is loaded once into memory before the per-row loop; each row's
   check runs against that in-memory list, which also grows with entries posted earlier in the same
   batch — so a within-file near-duplicate (two similar rows in one statement) is caught too, not
   just cross-file/cross-source ones. This is a superset of what FR-110 asked for, at no extra query
   cost.
4. **No resolution UI or endpoint this slice — recorded, not built.** Same reasoning as Decision
   0006's CONFIRM-stage deferral: a `Pending`/`ConfirmedDuplicate`/`ConfirmedDistinct` status column
   exists in `ingestion.duplicate_candidate` (migration 002), but only `INSERT`/`SELECT` are
   granted — there's no `UPDATE` path yet because there's no UI to drive one. Adding resolution
   means a new migration granting `UPDATE (status)`, not editing this one.

## Consequences

- `JournalEntryPosted` (Ledger.Contracts) gained a `Description` field it didn't have before — fuzzy
  matching needs the text to compare, and the published event had never needed it until now. Purely
  additive; every existing constructor call site was updated in the same change.
- The similarity heuristic is deliberately simple and will misfire on genuinely unrelated short
  strings that happen to be substrings of each other (e.g., "AB" inside "ABACAXI DELIVERY") — an
  accepted false-positive risk given the outcome of a false positive is "queued for a human to
  glance at and dismiss," not data corruption. A stricter algorithm can replace this one without
  touching anything upstream, since `StringSimilarity.Compute` is the sole seam.
- `ingestion.duplicate_candidate` rows accumulate with no expiry or cleanup — acceptable at current
  scale (mirrors `import_batch`'s own audit-trail posture), revisit if it ever needs one.
