# Decision 0012 — OFX import: FITID as idempotency key, tag-scanning parser, no ColumnMapping refactor

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [FR-108](../01-product/02-functional-requirements.md),
[Decision 0006](0006-csv-import-skips-confirm-stage.md)

## Context

FR-108 ("Import OFX statements") is the entire spec — Should-have priority, no worked example, no
user story, no field-mapping detail, unlike FR-107 (CSV) which came with US-010's concrete scenarios.
Three implementation questions needed resolving without that guidance:

1. **OFX has no user-defined column mapping** (unlike CSV's `ColumnMapping`) — its fields are
   semantically typed (`DTPOSTED`, `TRNAMT`, `FITID`, `NAME`/`MEMO`), not positional. Reusing
   `EntryProposalBuilder.FromParsedRow` — which only ever reads `ColumnMapping.PrimaryAccountId`/
   `.UnclassifiedAccountId`/`.Commodity`, never the four column-index/header fields — meant either
   refactoring `ColumnMapping` into a shared base plus a CSV-specific extension (touching five
   already-shipped, CI-proven files for a Should-have feature), or building an inert `ColumnMapping`
   for the OFX path with dummy column-index values that are provably never read.
2. **OFX 1.x is SGML "tag soup"** — leaf tags like `<DTPOSTED>` routinely have no closing tag at
   all — while OFX 2.x is well-formed XML. A real bank export could be either.
3. **What should BR-103's idempotency key hash?** CSV hashes the raw line text; OFX's `FITID` is the
   bank's own stable, unique-per-transaction identifier, purpose-built for exactly this.

## Decisions

**Reuse `EntryProposalBuilder` via an inert `ColumnMapping`, not a refactor.** `ImportOfxHandler`
builds `new ColumnMapping(primaryAccountId, unclassifiedAccountId, commodity, 0, 0, 0, false)` and
passes it to the same `EntryProposalBuilder.FromParsedRow` CSV already uses — verified, at the call
site, that those four fields are never read by that method. A "rule of three": two statement formats
sharing three real fields doesn't yet justify restructuring an already-tested type; a third format
would.

**One parser, one extraction technique for both OFX generations.** `OfxParser` finds each
`<STMTTRN>...</STMTTRN>` block via regex, then extracts each leaf value by scanning forward from
`<TAG>` to the next `<` — a closing tag, a sibling's opening tag, or a newline — whichever the file
actually has. This works identically whether or not `</DTPOSTED>` exists, so the parser never needs
to know or detect which OFX generation it's reading.

**`ParsedRow.RawLine` is the transaction's `FITID`**, not the full `<STMTTRN>` block, when a `FITID`
is present (falling back to the block only if one is somehow missing). BR-103's idempotency key
hashes `sourceId + rawRecord` — `FITID` is a *better* `rawRecord` than CSV's raw-line text for this
purpose, being immune to whitespace/formatting drift between two exports of the same overlapping
statement window, which is precisely the scenario FR-109's re-import dedup guarantee exists for.

**No CONFIRM-stage gate, same as CSV (Decision 0006)** — every row that parses posts directly.

## Consequences

- `ImportOfxHandler` duplicates roughly 60 lines of `ImportCsvHandler`'s archive/post/dedup/record
  pipeline rather than sharing it through an extracted base class or delegate. This is deliberate
  duplication, not an oversight — see the "rule of three" reasoning above. If a third import format
  (an aggregator feed, per ADR-0010's "M5 or later") arrives, extracting a shared
  `ImportPipeline<TParseFn>` at that point is the trigger, not before.
- "Row number" in an OFX `ParseFailure`/`ParsedRow` is a sequential transaction index within the
  file, not a file line number the way CSV's is — OFX isn't line-oriented. Documented at the call
  site so a future reader doesn't expect it to match a text editor's line count.
- The `Column` field name inside the reused `ColumnMapping` type is misleading for the OFX call
  site (there are no columns) — accepted as the cost of not refactoring a shipped type, and called
  out explicitly in `ImportOfxHandler`'s own comment so it doesn't read as a mistake.
