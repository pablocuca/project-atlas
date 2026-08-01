# ADR-0016 — Progression rewards process only, never outcomes

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Product, UX

## Context

The brief asks for professional, engineering-flavoured gamification: streaks, XP, levels, health
scores, velocity metrics. Done naively, this creates two serious problems:

1. **Goodhart's law.** Rewarding contribution *frequency* optimises for frequency, not for the
   quality of the decision. A user can maximise a streak while making poor allocation choices.
2. **Punishing noise.** Any score that moves with portfolio value drops when the market drops. That
   punishes the user for something they do not control, and — worse — it teaches them that the
   entire scoring system is arbitrary, which contaminates the credibility of the Health Score and
   the North Star alongside it.

## Decision

Progression may reward **only** behaviours that are (a) fully under the user's control and (b)
causally linked to Financial Independence. Returns, portfolio value, and market movement are
**structurally invisible** to the Progression module: its compile-time dependency set excludes
Valuation, Forecast, and Market Data. Streaks break only through user action or inaction.

Additionally, no mechanic may create urgency to open the app — no streak-loss countdowns, no
expiring rewards (Law 10, NG-11).

## Rationale

- Structural enforcement beats intent. A dependency rule cannot be forgotten during a late-night
  feature addition; a guideline can.
- Process metrics are the only honest thing to gamify, because they are the only thing the user
  actually controls.
- Removing the *ability* to observe returns removes the *temptation* to use them.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Reward portfolio milestones | Motivating; visible | Rewards luck; punishes bear markets; encourages checking during volatility | Directly harmful |
| Reward returns vs a benchmark | Engineering-flavoured; competitive | Encourages performance-chasing, the single most destructive retail behaviour | Actively harmful |
| No gamification at all | Zero risk of distortion | Loses genuine motivational value for consistency, which does matter | Process gamification is safe and useful |
| Guideline-only enforcement | Flexible | Erodes silently under feature pressure | Structural enforcement is nearly free here |

## Consequences

### Positive
- Progression can never punish the user for market behaviour.
- The entire module is removable with zero financial impact (BR-804), verified in CI.
- The Discipline Index measures adherence to the user's **own** declared policy, not a
  system-preferred behaviour.

### Negative — accepted costs
- Less immediately exciting than portfolio-milestone celebration.
- Some genuinely motivating moments (crossing a net-worth threshold) are deliberately unavailable
  in the progression system. They remain visible as facts, just not as rewards.

## Reversal cost

**Low technically, high in principle.** Reversal would require deleting MR-8 — a visible,
reviewable act.

## Compliance

MR-8, INV-160..INV-162, BR-800..BR-805. `no-frills build` proves removability.

## References
[Gamification Strategy](../../01-product/06-gamification-strategy.md) · [Product Philosophy Law 11](../../00-foundation/03-product-philosophy.md)
