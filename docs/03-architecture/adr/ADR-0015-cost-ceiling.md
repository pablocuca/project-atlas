# ADR-0015 — US$30/month infrastructure cost ceiling as an architectural constraint

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, SRE

## Context

Atlas must run for two decades. Personal projects do not usually die from technical failure — they
die from **accumulated friction**, and a recurring bill that feels disproportionate is a large,
compounding source of friction. A US$150/month personal system has a lifespan measured in
motivation; a US$20/month one survives job changes, indifference, and years of dormancy.

## Decision

Total production run-rate is capped at **US$30/month**, treated as a hard architectural constraint
with the same standing as a security or correctness requirement. Cost is an SLI; breaching it
declares a SEV-3 incident with a runbook. Every PR touching infrastructure must state its cost delta.

## Rationale

- **Low idle cost is a durability requirement.** For a two-decade system, this is a first-class
  quality attribute, not frugality.
- The constraint produces better architecture, not worse: scale-to-zero forces stateless handling;
  avoiding always-on middleware forces the outbox pattern; per-GB telemetry pricing forces
  cardinality discipline. Each is what a well-built system would do anyway.
- It forces every infrastructure choice to be justified rather than defaulted.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| No explicit ceiling | Freedom to choose "best" services | Costs drift upward invisibly; the classic path to abandonment | Unbounded cost is an existential risk here |
| US$100+/month, fidelity first | Full Azure-native reference architecture; better learning vehicle | ~US$15k over 20 years for capabilities N=1 never needs | Poor value; raises abandonment risk |
| Free tier only (~US$0) | Maximum durability | Forces Azure SQL over Postgres, violating AI-3; free tiers change unilaterally | Portability outranks the last few dollars |

## Consequences

### Positive
- Every infrastructure decision is deliberate and documented.
- Cold-start latency is accepted honestly rather than paid around.
- Headroom (~US$6–15/month) is explicitly reserved for simulation growth.

### Negative — accepted costs
- No staging environment (`RISK-014`).
- Cold starts up to ~8s p95 — acceptable for a system used a few times per week.
- Some Azure-native services (Service Bus, App Insights, ACR) are excluded, and the seams that
  preserve them cost a little extra design work.

## Reversal cost

**None.** Raising the ceiling is a decision, not a migration. The seams (`IEventBus`, OTLP) make
adopting paid services straightforward if the calculus ever changes.

## Compliance

Azure Budget at US$30 with alerts at 50/80/100%. `atlas_monthly_cost_usd` metric. PR template
requires a cost delta; "unknown" is rejected.

## References
[Infrastructure](../08-infrastructure.md) · [Non-Functional Requirements](../../01-product/03-non-functional-requirements.md)
