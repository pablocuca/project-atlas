# ADR-0023 — Bilingual product surface over an English-only specification

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Product

## Context

Atlas must serve a Brazilian user in Portuguese while remaining legible to an English-speaking
engineering audience — and, if open-sourced, to contributors who do not read Portuguese. "Bilingual"
is ambiguous across at least four surfaces, and treating them uniformly is wrong in both directions:

| Surface | Naive answer | Actual consequence |
|---|---|---|
| App UI | Translate it | Correct |
| Regulatory terms | Translate them | **Wrong** — a translated legal term loses its referent |
| Code and identifiers | Translate them? | Wrong — breaks universal practice and OSS viability |
| The 77,000-word specification | Translate it | Two copies drift; a stale spec actively misleads |

There is also a non-obvious compliance dimension. The advice boundary in
[ADR-0022](ADR-0022-advice-posture.md) is enforced mechanically by a copy lint carrying an English
banned-construction list. Adding Portuguese UI copy without a Portuguese banned list would leave the
CVM boundary enforced in one language and unenforced in the other — a gap invisible to review,
because the lint would still pass.

## Decision

**The product surface is bilingual (pt-BR default, en available). The specification, codebase,
identifiers, telemetry, and interchange formats are English-only.**

Terminology follows a three-tier policy:

1. **Regulatory terms are never translated**, in any locale (`come-cotas`, `DARF`, `isenção`,
   `tabela regressiva`, `PGBL`, …), and carry an English gloss on first use.
2. **Domain terms are translated** against a fixed, versioned canonical pairing.
3. **SRE vocabulary stays English inside Portuguese prose** (`SLI`, `SLO`, `burn rate`, `runbook`),
   because that is how the audience actually speaks.

Locale becomes a **server** concern: `FactSet.displayString` is locale-keyed and formatted
server-side. The copy lint, ICU completeness checks, narrative golden corpora, and traceability
verification all run **per locale**.

## Rationale

- **English canonical spec eliminates drift.** A stale translated specification is worse than none,
  because decisions get made against it and nobody notices until the decision is wrong.
- **Regulatory terms are pointers to legal referents.** "Semi-annual fund withholding" is a
  description that resembles come-cotas; it is not come-cotas, and it drifts when the law changes.
- **Locale on the server preserves "no business logic in the client"** — a rule that keeps a future
  client rewrite cheap ([ADR-0013](ADR-0013-flutter-client.md)) and puts formatting under the same
  golden-file discipline as everything else.
- **Per-locale linting closes the compliance gap** before it exists, rather than after an audit.
- **Generating narrative per locale, rather than translating it**, keeps the traceability guarantee
  (BR-901) intact — translated prose would break token-to-fact matching and make a safety property
  depend on a translator.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Fully bilingual, spec included | Strongest for a Brazilian OSS audience | Doubles every doc edit forever; drift near-certain; a stale spec misleads | Maintenance burden and drift risk exceed the reach benefit |
| English canonical + curated PT subset | Some reach, bounded cost | Still two artifacts to keep in sync; staleness headers are a partial mitigation, not a fix | Explicitly considered and declined by the owner |
| Portuguese-only product and spec | Simplest; matches the sole user | Forecloses open-sourcing; excludes English-reading contributors; unusual for code | Loses optionality for no gain |
| Translate all terms including regulatory | Internally consistent | Loses legal referents; makes the tax engine unreviewable by a professional | Correctness failure |
| Machine translation at runtime | Cheap; scales to any locale | Untraceable, unreviewable, **bypasses the copy lint entirely** | Would silently break the legal posture |

## Consequences

### Positive
- The specification stays a single source of truth with zero drift risk.
- The advice boundary is enforced identically in both languages.
- Formatting correctness is tested server-side under existing golden-file discipline.
- A third locale is purely additive: catalogue + banned lists + golden files.
- The tax engine remains reviewable by a Brazilian professional (citations stay Portuguese).

### Negative — accepted costs
- A Brazilian open-source contributor meets an English-only 77,000-word specification on arrival.
  Accepted deliberately; the README carries a short Portuguese orientation paragraph as partial
  mitigation.
- Narrative golden corpora, formatting fixtures, and copy-lint lists all double.
- Portuguese text expansion (~15–25%) constrains layout; all surfaces must be tested at both locales
  at maximum Dynamic Type.
- Every user-facing string must be a full ICU message — string concatenation is banned, because
  Portuguese gender and number agreement break concatenated fragments in ways English does not
  reveal.

## Reversal cost

**Low in one direction, high in the other.** Adding a locale is additive. *Removing* bilingual
support would be trivial. Translating the specification later remains possible at any time; having
translated it and let it drift would not be recoverable.

## Compliance

BR-B01..B06 · FR-940..945 · NFR-810..814. Per-locale copy lint, ICU completeness gate, and
per-locale narrative golden corpora all run in CI. A locale without a banned list fails the build.

## References
[Localisation Strategy](../../01-product/11-localisation-strategy.md) · [ADR-0022](ADR-0022-advice-posture.md) · [Ubiquitous Language](../../00-foundation/05-ubiquitous-language.md)
