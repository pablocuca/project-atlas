# Non-Goals

**Status:** Ratified · **Owner:** CTO / Product · **Last reviewed:** 2026-08-01

A specification is defined as much by its refusals as its commitments. Each non-goal below has a
**reason** and a **reconsideration trigger** — the condition under which it would be revisited.
Absent that trigger, the answer is no, permanently.

---

## NG-01 — Atlas is not a budgeting app

**Refused:** envelope budgeting, per-category monthly limits, spending guilt loops, "you overspent
on dining" notifications.

**Why:** budgeting optimises the wrong variable. On a twenty-year horizon, savings *rate* and
income trajectory dominate category-level discipline by an order of magnitude. Atlas models
spending as a **stochastic process to be forecast**, not a set of limits to be policed.

**Instead:** the Discretionary Error Budget — a monthly allowance of overspend expressed in
*FI-days cost*, with burn-rate alerting. Same discipline, correct unit, no moralising.

**Reconsider if:** attribution consistently shows category-level spend as the top controllable
driver for three consecutive quarters.

---

## NG-02 — Atlas is not a portfolio tracker

**Refused:** headline daily P&L, ticker tickers, "top movers", real-time quote streaming, red/green
day framing.

**Why:** [Law 2](03-product-philosophy.md). Daily return is stochastic at this horizon. Displaying
it as a primary surface is actively harmful.

**Instead:** positions exist as ledger truth; valuation is a projection consumed by the twin.
Market movement is visible on demand, classified as variance, and never alerts.

**Reconsider if:** never. This is a Foundation-level refusal.

---

## NG-03 — Atlas is not a bank, broker, or payment system

**Refused:** holding funds, initiating transfers, executing trades, issuing cards, PIX initiation.

**Why:** this converts a personal analytics system into a regulated payment institution under
BACEN, with capital, audit, and licensing obligations that would end the project. The value of
Atlas is entirely in *deciding*, not in *executing*.

**Reconsider if:** never, in this product. A separate product with separate legal structure would
be required.

---

## NG-04 — Atlas does not give investment advice

**Refused:** "buy X", "sell Y", specific security recommendations, allocation prescriptions
presented as instructions.

**Why:** in Brazil, personalised securities recommendation is a regulated activity (CVM
Resolution 19 — consultoria de valores mobiliários). Beyond legality, [Law 14](03-product-philosophy.md)
holds that unexamined trust in a model is the failure mode to design against.

**Instead:** ranked options with quantified FI-date and risk-band impact, stated assumptions, and
an explicit list of what the model does not know.

**Reconsider if:** the operator obtains CVM registration. Even then, Law 14 governs presentation.

---

## NG-05 — Atlas is not a social or comparative product

**Refused:** leaderboards, peer benchmarking, "users like you", sharing, social feeds.

**Why:** financial comparison degrades decision quality and creates privacy surface with no
corresponding benefit. The only valid benchmark is **the user's own prior trajectory**.

**Reconsider if:** never.

---

## NG-06 — Atlas is not real-time

**Refused:** streaming prices, sub-minute recomputation, live tickers, push-on-price-move.

**Why:** the decision cadence of financial independence is measured in weeks. Real-time
infrastructure would multiply cost, complexity, and noise simultaneously — the worst possible
trade. Freshness targets are stated in [NFRs](../01-product/03-non-functional-requirements.md) and
measured in hours, not seconds.

**Reconsider if:** a use case emerges where a sub-daily decision materially changes `t_FI`. None
is currently known.

---

## NG-07 — Atlas is not multi-tenant SaaS (yet)

**Refused:** signup flows, billing, per-tenant support, tiered plans.

**Why:** [ADR-0011](../03-architecture/adr/ADR-0011-single-tenant-core.md). Multi-tenancy taxes
every query, migration, and test from day one, and would be paid for years before any second user
exists. The *seams* are built; the *machinery* is not.

**Reconsider if:** the decision is made to open-source and self-hosting demand appears. The seam
design makes this a project, not a rewrite.

---

## NG-08 — Atlas does not chase feature parity

**Refused:** matching what YNAB, Mobills, Kinvo, Personal Capital, or Monarch ship.

**Why:** those products answer *"what happened"*. Atlas answers *"what is likely, why, and what
now"*. Parity thinking would consume the budget that belongs to the tax engine, attribution, and
calibration — the three things nobody else has.

---

## NG-09 — Atlas will not use an LLM as a calculator

**Refused:** LLM-derived figures, LLM-estimated projections, LLM "reasoning" over raw numbers as an
output path, agentic financial actions.

**Why:** [Law 5](03-product-philosophy.md). A confidently wrong financial number is worse than no
number. Language models narrate; they do not compute.

**Reconsider if:** never. This is enforced by architecture (the narration layer receives only a
validated fact set and cannot reach the ledger).

---

## NG-10 — Atlas does not model what it cannot ground

**Refused:** crypto yield farming projections, alternative-asset return models with no defensible
prior, "market timing" signals, technical analysis.

**Why:** a model with no empirical grounding produces false precision that contaminates the North
Star and destroys calibration. Assets Atlas cannot model probabilistically are held in the ledger
at cost or mark, **excluded from the forecast distribution**, and shown as an explicit
"unmodelled" bucket.

---

## NG-11 — Atlas does not optimise for engagement

**Refused:** streak-preservation nudges, daily check-in prompts, gamified retention loops,
notification-driven return visits.

**Why:** [Law 10](03-product-philosophy.md). Engagement and the mission are in direct opposition.
Success is measured in attention returned to the user, not captured from them.

---

## Summary table

| ID | Non-goal | Level |
|---|---|---|
| NG-01 | Budgeting app | Product |
| NG-02 | Portfolio tracker | **Foundation — permanent** |
| NG-03 | Bank / broker / payments | **Foundation — permanent** |
| NG-04 | Investment advice | Legal |
| NG-05 | Social / comparative | **Foundation — permanent** |
| NG-06 | Real-time | Architecture |
| NG-07 | Multi-tenant SaaS | Deferred by ADR |
| NG-08 | Feature parity | Strategy |
| NG-09 | LLM as calculator | **Foundation — permanent** |
| NG-10 | Ungrounded models | Integrity |
| NG-11 | Engagement optimisation | **Foundation — permanent** |
