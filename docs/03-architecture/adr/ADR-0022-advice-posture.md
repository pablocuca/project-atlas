# ADR-0022 — Ranked options with disclosed tradeoffs, not prescriptions

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Product, Legal posture

## Context

The founding brief asks the system to answer *"what should I do next?"*. There are three defensible
postures, and they differ in both usefulness and legal exposure:

1. **Observability only** — surface facts, never suggest. Safest; abandons the question.
2. **Ranked options with tradeoffs** — evaluate several actions, quantify each, rank, disclose
   unknowns. The user decides.
3. **Prescriptive** — name one action. Highest perceived utility; constitutes personalised
   investment advice, a regulated activity in Brazil (CVM Resolution 19).

There is also a behavioural dimension independent of law: a single prescription invites unexamined
trust in a model whose own reliability is uncertain — the precise failure the Calibration loop
exists to prevent.

## Decision

Atlas presents **2 to 5 evaluated options**, ranked, each with quantified FI-date and risk impact,
after-tax cost, reversibility, effort, and an **explicitly non-empty list of what the model does not
know**. It never issues an instruction, and never names a specific security.

## Rationale

- It genuinely answers the user's question while leaving the decision — and therefore the
  responsibility and the understanding — with them.
- Disclosure of unknowns is what separates decision support from advice, both ethically and
  practically.
- Ranking is honest: it conveys the system's assessment without pretending to certainty it does not
  have.
- The minimum of two options is deliberate. A single "option" is a prescription wearing a costume.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Observability only | Zero legal exposure; simplest | Abandons the core question; leaves the hardest work to the user | Fails the mission |
| Prescriptive single action | Highest perceived utility; least cognitive load | Regulated activity; invites unexamined trust; catastrophic when the model is wrong | Legal and behavioural risk both unacceptable |
| Options with no ranking | Maximally neutral | Unhelpfully evasive; the system does have an assessment and hiding it is not honesty | Neutrality theatre |
| Options with security-level detail | Actionable | Squarely regulated securities recommendation | Prohibited by NG-04 |

## Consequences

### Positive
- The mission's hardest question is answered without overreach.
- Users learn the *structure* of their tradeoffs, not just an answer.
- Degraded forecast reliability can suppress options with a stated reason — a graceful, honest
  failure mode that a prescriptive system cannot offer.

### Negative — accepted costs
- Higher cognitive load than being told what to do. Mitigated by ranking and clear presentation.
- Requires counterfactual evaluation of every option — real compute cost, and the main driver of
  the simulation budget.
- Some users will find it evasive. Accepted, and recorded as a bet (`B6`) that may prove wrong.

## Reversal cost

**Low technically, high legally.** Becoming prescriptive would require CVM registration and a
different product posture entirely.

## Compliance

INV-150..INV-154, BR-600..BR-609, NG-04. A copy lint bans imperative advice phrasing in option text.
`notModelled` is required to be non-empty at runtime.

## References
[Recommendation Engine](../../04-engines/05-recommendation-engine.md) · [Compliance & Legal Posture](../../06-governance/03-compliance-and-legal-posture.md) · [Product Philosophy Law 14](../../00-foundation/03-product-philosophy.md)
