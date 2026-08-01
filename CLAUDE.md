# Agent-facing conventions

This is Project Atlas: a specification-first, decade-scale personal-finance system. Full context:
[`docs/README.md`](docs/README.md).

## Before writing any code

1. **Read the governing document first.** Every module maps to a bounded context in
   [`docs/02-domain/02-bounded-contexts.md`](docs/02-domain/02-bounded-contexts.md); every rule you
   enforce should trace to a numbered `FR-`/`BR-`/`INV-`/`NFR-` identifier. If the document doesn't
   exist yet, write it before the code ([ADR-0019](docs/03-architecture/adr/ADR-0019-docs-precede-code.md)).
2. **Precedence order** when documents seem to conflict: Foundation > Product > Domain > Architecture
   > Engines > Engineering. A lower layer may only contradict a higher one via an approved ADR.
3. **Locked decisions** (Brazil-only tax, single-tenant core with OSS seams, ≤US$30/mo cloud,
   ranked-options advice posture, bilingual product/English-only spec) are recorded in
   `docs/README.md` and are not open for re-litigation without a new ADR.
4. **Check the roadmap** ([`docs/01-product/08-roadmap-and-milestones.md`](docs/01-product/08-roadmap-and-milestones.md))
   for the current milestone and its exit gate. Milestones are dependency-sequenced, not scheduled —
   M0 (bitemporal double-entry ledger) comes first, and nothing above it in the roadmap should be
   started before its exit gate is met.

## While writing code

- [Coding Standards](docs/05-engineering/02-coding-standards.md) — the ten non-negotiables (CS-1..10)
  are analyser-enforced; read them before writing `*.Domain` code.
- [Modular Monolith](docs/03-architecture/03-modular-monolith.md) — module reference rules MR-1..10.
  `X.Domain` references only `Atlas.Kernel`. Nothing else, ever.
- [Testing Strategy](docs/05-engineering/03-testing-strategy.md) — every `BR-` needs a test annotated
  with its identifier; the rule-coverage gate fails the build otherwise.
- [Definition of Done](docs/05-engineering/04-definition-of-done.md) — the exit checklist for any PR.

## Decisions made during implementation

Domain-model-level gaps the specification left open get resolved and recorded in `docs/decisions/`
(numbered, referencing the ADR they extend) — not silently encoded in code. See
[`docs/decisions/`](docs/decisions/) for the log.
