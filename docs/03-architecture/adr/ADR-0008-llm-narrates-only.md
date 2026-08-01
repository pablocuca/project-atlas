# ADR-0008 — Deterministic engine computes; LLM only narrates

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, AI Architect

## Context

Atlas must explain complex financial situations in plain language, and LLMs are excellent at that.
They are also capable of producing a confident, wrong number — and in a system whose entire value
rests on trustworthy figures, a single hallucinated monetary amount destroys the credibility of
every other number on the screen.

The temptation is real and worth naming: it would be much easier to hand the model a ledger dump
and ask it to summarise.

## Decision

The deterministic engine computes **every** number. The LLM receives only a validated `FactSet` —
pre-computed, pre-formatted, pre-rounded facts with provenance — and renders prose over it. Every
numeric token in generated output must resolve to a `Fact.id`; unresolvable output is **rejected at
generation time** and the system falls back to structured display.

The Narrative module's compile-time dependency set contains only `FactSet` contracts. It cannot
reach the Ledger, Forecast, or Tax modules **even if someone tries**.

## Rationale

- The failure mode is asymmetric and severe: a wrong number is far worse than plain formatting.
- Traceability verification is cheap, mechanical, and complete — token matching against `Fact.id`
  is not a heuristic.
- Architectural enforcement beats prompt engineering. Prompts drift and models change; module
  dependency rules do not.
- It also makes the model swappable and the feature removable (BR-804) — the entire AI layer can be
  deleted without affecting a single financial output.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| LLM computes with tool access | Flexible; conversational | Introduces a non-deterministic path to a financial figure; unauditable; unreplayable | Violates AI-2 and Law 5 outright |
| LLM with code-interpreter sandbox | Verifiable arithmetic | Still non-deterministic in *what* it computes; irreproducible across model versions | Reproducibility, not arithmetic, is the requirement |
| Templates only, no LLM | Fully deterministic | Rigid; poor at multi-factor explanation, which is exactly what attribution needs | Loses genuine value where it is safe to have it |
| LLM post-hoc review of numbers | Might catch errors | Non-deterministic validation of deterministic output is strictly worse than a test | Inverted trust model |

## Consequences

### Positive
- A hallucinated figure is structurally impossible, not merely unlikely.
- The AI provider, model, and prompts are all swappable with no financial risk.
- Prompts contain minimal personal data (BR-906), so the privacy surface is small.

### Negative — accepted costs
- Narrative flexibility is limited to what the `FactSet` contains. New phrasings often require new
  facts — a real, recurring cost.
- Building a good `FactSet` is engineering work that a naive design would skip.
- Some genuinely useful conversational analysis is off the table. Accepted.

## Reversal cost

**Low to reverse the restriction; catastrophic to trust if reversed.** Technically trivial;
strategically a Foundation-level refusal (NG-09).

## Compliance

BR-900..BR-906, INV-170..INV-172, MR-9. Traceability verification runs at generation time. Golden
corpus regression on any prompt or model change. `no-frills build` proves removability.

## References
[AI Strategy](../../06-governance/02-ai-strategy.md) · [Product Philosophy Law 5](../../00-foundation/03-product-philosophy.md)
