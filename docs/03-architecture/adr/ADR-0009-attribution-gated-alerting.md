# ADR-0009 — Attribution-gated alerting; stochastic drivers never alert

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Product

## Context

The founding brief offered this as an example of a *good* card:

> "Time gained toward Financial Independence: 4 days"

It is the most dangerous idea in the brief, and it deserves a permanent record of why it was
rejected in that form.

With a ~15% annualised portfolio volatility, daily market noise moves the FI date by **an order of
magnitude more** than a month of disciplined saving. A system reporting that movement as an event
would:

- train the user to react to noise, which is precisely the behaviour that destroys long-term returns;
- make genuine signals — a salary change, a spending regime shift — indistinguishable from static;
- generate alert fatigue until the product is muted, then abandoned.

The SRE framing in the brief is exactly the right corrective, if taken seriously: this is the
alerting-on-noise problem, and SRE solved it with burn rates, multi-window evaluation, and
significance.

## Decision

**No delta reaches the user until it has been attributed**, and drivers classified `Stochastic`
**can never produce a `ChangeEvent`, notification, or headline change.**

Every delta decomposes into:
- **Controllable** — user decisions (spending, saving, income action, debt action, allocation)
- **Structural** — exogenous but persistent (rates, inflation, tax law, mortgage reset)
- **Stochastic** — market marks

Only the first two can pass the Signal Gate, and only then if they also clear materiality
(≥ 30 FI-days or ≥ 1pp `P(FI)`), statistical significance (> 2σ of the trailing 90-day noise band),
and deduplication. Stochastic movement consumes a **Variance Budget**, which is informational and
never becomes an incident.

Enforcement is structural: the Signal Gate's input type cannot represent a stochastic-only delta.

## Rationale

- This is the difference between a Mission Control and an anxiety machine, and it is the single
  highest-leverage design decision in the product.
- Attribution is computable: the counterfactual machinery already exists for Advisory, so
  decomposing a delta by holding drivers fixed is nearly free.
- Suppressed deltas are retained and inspectable, so nothing is hidden — it is simply not shouted.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Show all deltas | Transparent; simple | Noise dominates; trains reactive behaviour; the standard failure of this product category | Actively harmful |
| Threshold-only filtering | Simple to implement | A large market move passes any absolute threshold while being pure noise | Filters magnitude, not meaning |
| User-configurable sensitivity | Respects autonomy | Pushes a hard statistical judgement onto the user; most will get it wrong | Defaults must be correct; tuning is secondary |
| Daily digest of everything | Bounded frequency | Still presents noise as content, just batched | Same failure, on a timer |

## Consequences

### Positive
- The product's default state is silence, and speech is earned (Law 9).
- When Atlas does speak, it is worth reading — which is what makes it trusted.
- The suppress:emit ratio becomes a measurable product-health metric.

### Negative — accepted costs
- The user may occasionally be surprised by cumulative market movement they were never alerted to.
  Mitigated by the always-visible FI band and the on-demand variance view.
- Attribution is expensive to compute and hard to get exactly right (Shapley over interacting
  drivers). Mitigated by the ≤2% residual invariant, which fails loudly rather than silently.

## Reversal cost

**Low technically, high in product terms.** Loosening the gate is a config change. Doing so would
convert Atlas into the category of product it was explicitly built not to be.

## Compliance

INV-130..INV-134, BR-200..BR-216. Signal Gate input type structurally excludes stochastic-only
deltas. `atlas_signal_gate_decisions_total` is monitored as a product-health metric.

## References
[Attribution Engine](../../04-engines/04-attribution-engine.md) · [Product Philosophy Law 2 & 9](../../00-foundation/03-product-philosophy.md) · [Dashboard Strategy](../../01-product/05-dashboard-strategy.md)
