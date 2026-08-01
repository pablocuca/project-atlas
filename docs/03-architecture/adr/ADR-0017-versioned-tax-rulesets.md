# ADR-0017 — Tax rules as versioned data with effective dates

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Domain

## Context

Brazilian tax law changes — rates, thresholds, exemption limits, and occasionally the structure of
a regime. Two requirements collide:

1. Today's tax must be computed under today's law.
2. A forecast produced in 2026 must remain **replayable in 2034 under 2026 law**, or historical
   comparison and calibration become meaningless (AI-2, ADR-0006).

Encoding rules directly in code fails requirement 2: a deployment that updates rates silently
changes the meaning of every historical forecast.

## Decision

Tax rules are **versioned data with effective date ranges**, not code. `ITaxJurisdiction` resolves
the ruleset applicable to a given date, and every `TaxConsequence` records the `RulesetVersion` used.
Historical forecasts replay under the ruleset effective at their computation date. A retroactive
legal correction is a **new ruleset version**, never an edit to an existing one.

## Rationale

- Separating rules from code makes the "law changed" event a data change with a reviewable diff.
- Effective-date resolution is the only mechanism that satisfies both requirements simultaneously.
- Recording the ruleset version in every consequence makes any historical figure explicable.
- Every rule cites its legal basis (BR-407), which makes review by a tax professional tractable.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Rules in code, updated by deploy | Simple; type-safe | Silently changes historical meaning; no replay | Breaks AI-2 |
| Rules in code with `if (year >= …)` branches | Keeps history | Unmaintainable within a few years; untestable combinatorics | Degrades rapidly |
| External tax service | Always current; someone else's problem | Opaque, unversionable, unreplayable, network dependency in a pure module | Violates purity and replayability |

## Consequences

### Positive
- Any past figure is explicable under the law as it stood.
- Law changes are a reviewable data diff, testable against golden fixtures.
- Rulesets can be reviewed by a domain expert without reading code.

### Negative — accepted costs
- A ruleset schema must be designed to be expressive enough for real Brazilian rules without
  becoming a general-purpose programming language. This is genuinely hard and is the main design
  risk in the tax context.
- Ambiguous rules must resolve conservatively and flag for review (BR-408).

## Reversal cost

**High.** Rulesets and their historical versions are load-bearing for replay.

## Compliance

BR-402, BR-403, BR-407, INV-051. Golden-file tests per ruleset version. Annual calendar-triggered
review, plus on any legislative change.

## References
[Tax Engine — Brazil](../../04-engines/07-tax-engine-brazil.md) · [ADR-0014](ADR-0014-brazil-only-tax-engine.md)
