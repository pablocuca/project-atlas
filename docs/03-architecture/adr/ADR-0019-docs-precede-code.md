# ADR-0019 — Specification precedes implementation; documentation lives in the repository

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO

## Context

Atlas is intended to be maintainable for decades, potentially by someone who has never spoken to
its author. The dominant failure mode of long-lived personal projects is not bad code — it is
**lost rationale**: code that works, that nobody dares change, because nobody remembers why it is
shaped that way.

## Decision

No production code is written for a capability until the document governing it exists and is
ratified. All documentation lives **in the repository**, versions with the code, and changes only by
pull request. Requirement, rule, and invariant identifiers (`FR-`, `NFR-`, `BR-`, `INV-`) are
permanent and are cited in tests and commit messages.

## Rationale

- Writing the specification is where the thinking happens; skipping it defers the thinking to a
  point where it is expensive to act on.
- In-repo docs cannot drift out of sync with a release, and are reviewable with the same tooling as
  code.
- Stable identifiers create a navigable chain: requirement → rule → test → commit → deployed
  behaviour. That chain is what makes the system intelligible in 2034.
- The specification is the deliverable that a stranger reads first. It must be complete before the
  code it governs.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Code first, document after | Fast start; feels productive | Documentation is never written, or is written as archaeology; rationale is lost immediately | The failure this ADR exists to prevent |
| External wiki (Notion, Confluence) | Nicer editing; easier linking | Drifts from the code; not versioned with releases; dies with the tool or subscription | Violates the decade horizon |
| Docstrings only | Close to the code | Cannot express architecture, strategy, or rejected alternatives | Wrong granularity for the decisions that matter |

## Consequences

### Positive
- Every decision has a written rationale, including the alternatives rejected.
- Onboarding — including future-self onboarding after a two-year gap — is tractable.
- The rule-coverage CI gate is possible because rules have stable identifiers.

### Negative — accepted costs
- Slower start. Explicitly accepted: "never optimise for speed."
- Documentation maintenance is ongoing work, and stale docs are worse than none. Mitigated by the
  rule-coverage gate and by review requirements on identifier changes.

## Reversal cost

**Not applicable** — this is a process decision, revisited only by an ADR superseding it.

## Compliance

PR template requires the governing document reference. Rule-coverage CI gate parses
`05-business-rules.md` and fails on any `BR-` without a matching annotated test.

## References
[Contributing Guide](../../05-engineering/06-contributing.md) · [Definition of Done](../../05-engineering/04-definition-of-done.md)
