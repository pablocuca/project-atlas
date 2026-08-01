# Vision

**Status:** Ratified · **Owner:** CTO · **Last reviewed:** 2026-08-01

---

## 1. The ten-year picture

By 2036, Project Atlas is the system through which one person reached Financial Independence
**deliberately rather than accidentally** — and can prove, with a decade of scored forecasts,
that the system's guidance was calibrated rather than confident.

Atlas is not consulted. It is *inhabited*. It runs continuously, it maintains a live model of a
financial life, and it speaks only when something has genuinely changed. The measure of its
success is not engagement — it is **the number of days per year the user needs to think about
money at all**, trending toward zero, while the probability of reaching Financial Independence
trends toward one.

## 2. The problem, stated precisely

Personal finance software answers *"what happened?"*. A minority answers *"what do I have?"*.
Essentially none answers the only question that matters over a twenty-year horizon:

> **"Given everything I know today, and everything I cannot know, am I still on the best available
> path to financial freedom — and if not, what specifically changed, how much did it cost me, and
> what are my options?"**

That question is hard for four reasons, and each one is a design constraint:

| Reason it is hard | Consequence for Atlas |
|---|---|
| The future is a distribution, not a number | Every output is probabilistic and carries its own uncertainty |
| Signal is buried in market noise | Attribution must precede display. Marks never raise alarms |
| Taxes and sequencing dominate the answer | A tax engine is core domain, not an afterthought |
| Models are unfalsifiable unless scored | Forecast calibration is a first-class, permanently-running subsystem |

## 3. What Atlas is

**A Digital Twin of a financial life, under continuous simulation, with an SRE-grade reliability
model wrapped around it, and a decision-support layer that quantifies the options.**

Four properties define it:

1. **It is a system of record before it is anything else.** A bitemporal, append-only, double-entry
   ledger. Everything else is a projection. If the ledger is wrong, nothing above it can be right.
2. **It simulates continuously.** Not on demand, not on refresh — the twin is re-forecast on every
   material state change, and forecasts are immutable artifacts that can be replayed years later.
3. **It attributes before it alerts.** No number is shown as a change until the system can say
   *what caused it* and *whether the cause was a decision, a structural shift, or noise*.
4. **It grades itself.** Every forecast is scored against reality. The system's own reliability is
   a displayed metric, and a degraded one suppresses its own advice.

## 4. What "won" looks like

Atlas has succeeded if, at any moment, the user can answer these without opening a spreadsheet:

- What is my probability of Financial Independence by my target date, and how wide is the band?
- What moved that probability this month, ranked by contribution, excluding market noise?
- Which of my behaviours is currently outside its operating envelope, and by how much?
- What are my three best available actions right now, with the FI-date and risk cost of each?
- How well-calibrated has this system historically been at the confidence level it is quoting me?

And one meta-condition: **the user trusts the number enough to act on it, because the system has
shown its work and its track record.**

## 5. Strategic bets

| # | Bet | If wrong |
|---|---|---|
| B1 | Attribution and noise-suppression are the core product, not forecasting | Atlas becomes another anxiety machine; users react to volatility |
| B2 | Forecast calibration creates durable trust no competitor can copy quickly | The differentiator collapses to UI polish |
| B3 | Brazilian tax modelling is deep enough to be a moat | FI dates are wrong by years and the system loses credibility |
| B4 | Manual-first ingestion makes the product useful before any integration exists | The project stalls waiting on aggregator contracts |
| B5 | A modular monolith reaches decade-scale maintainability faster than microservices | Scaling pain arrives earlier than expected — mitigated by module seams |
| B6 | Ranked-options advice is more trusted than prescriptive advice | The user wants to be told what to do and finds Atlas evasive |

## 6. Time horizon and posture

Atlas is built for **decades**, which imposes an unusual discipline:

- **The data outlives the code.** The ledger format is open, documented, and exportable to a
  vendor-neutral form at any moment. Azure is an implementation detail with an exit path.
- **The code outlives the framework.** Domain logic depends on nothing that could be deprecated.
  Infrastructure is at the edges.
- **The decisions outlive the memory.** Every non-obvious choice becomes an ADR. In 2034, the
  reason for a 2026 decision must be readable.
- **Never optimise for speed.** Optimise for the version of this system that a stranger must
  maintain in ten years without asking anyone a question.

## 7. Open-source ambition

Atlas is built single-tenant for one user, but every boundary that multi-tenancy would need
(identity, jurisdiction, secrets, currency) exists as a seam from the first commit. The intent is
that Atlas can be opened as a reference implementation of a **Financial Operating System** without
a data-layer rewrite. This is an option deliberately kept open, not a commitment to exercise it.

---

**See also:** [Mission & North Star](02-mission-and-north-star.md) · [Non-Goals](04-non-goals.md) · [Product Philosophy](03-product-philosophy.md)
