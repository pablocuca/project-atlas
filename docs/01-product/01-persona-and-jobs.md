# Persona & Jobs to Be Done

**Status:** Ratified · **Owner:** Product

---

## 1. The Operator

There is exactly one user, and designing for one real person rather than a market segment is a
deliberate advantage — it removes every compromise made to accommodate a hypothetical average user.

| Attribute | Value | Design consequence |
|---|---|---|
| Profession | Software / mobile / cloud engineer | Density is welcome; hand-holding is not |
| Mindset | Architecture-first, systems thinker | The SRE metaphor lands natively |
| Interests | SRE, DevOps, AI, Open Finance, long-term investing | The product's vocabulary is already theirs |
| Jurisdiction | Brazil | Tax depth is non-optional |
| Ecosystem | Apple, Azure, Flutter | iOS-primary client; Azure-native infra |
| Horizon | 15–20 years to FI | Daily volatility is irrelevant; decades matter |
| Numeracy | High | Distributions and probabilities can be shown directly |
| Time available | Scarce and irregular | Every interaction must earn itself |
| Failure mode | Over-engineering; analysis paralysis | The product must *reduce* decisions, not multiply them |

### What makes this user unusual, and what it permits

They will **read the specification**. They will notice if a probability has no band. They will be
suspicious of a number that cannot explain itself, and they will lose trust permanently if one turns
out to be wrong.

This permits things a mass-market product could never do — showing model uncertainty, admitting
degraded reliability, presenting Pareto fronts instead of a single answer. It also **requires** them:
this user's trust is earned by rigour, and lost by any hint of false precision.

### The risk this persona creates

The greatest product risk is not that Atlas is too complex — it is that **the Operator builds it
forever and never uses it**. An engineer building a tool for themselves optimises for the building.
The roadmap counters this deliberately: M0–M2 produce a *usable* system before any of the
intellectually interesting work begins, and every milestone gate is about verified behaviour rather
than completed features.

---

## 2. Jobs to Be Done

### JTBD-1 — "Tell me if I'm still on track, without me having to check"
> *When* my financial life changes in ways I may not notice,
> *I want to* be told only when something material has actually changed,
> *so I can* stop carrying the background anxiety of not knowing.

**Currently solved by:** periodic spreadsheet reviews, which are inconsistent and only happen when
already anxious.
**Success:** the user stops checking, because they trust they would be told.
**Served by:** North Star, Attribution, Signal Gate, Notification ceiling (NFR-808).

### JTBD-2 — "Tell me what actually changed, not what moved"
> *When* a number is different from last month,
> *I want to* know which of my decisions caused it and which was just the market,
> *so I can* learn from my choices instead of reacting to noise.

**Currently solved by:** nothing. No consumer product separates these.
**Success:** the user can answer "did I improve this year?" without a spreadsheet.
**Served by:** Attribution Engine, Controllable Drift, driver ranking.

### JTBD-3 — "Show me what my options cost"
> *When* I face a financial decision (mortgage vs invest, PGBL vs taxable, job change),
> *I want to* see the quantified tradeoffs in FI-days and tail risk,
> *so I can* decide deliberately rather than by intuition or forum consensus.

**Currently solved by:** ad-hoc spreadsheets that ignore tax and sequence risk.
**Success:** decisions are made with quantified alternatives and stated unknowns.
**Served by:** Recommendation Engine, Simulation Engine, Tax Engine.

### JTBD-4 — "Tell me what's broken before it's expensive"
> *When* part of my financial operation drifts out of its envelope,
> *I want to* know early, with a procedure to follow,
> *so I can* fix it while it is still cheap.

**Currently solved by:** noticing, eventually, usually late.
**Success:** drift is caught in weeks, not years.
**Served by:** Financial Reliability Model, SLOs, burn rates, runbooks.

### JTBD-5 — "Let me trust the number"
> *When* the system tells me I have an 83% probability,
> *I want to* know how often it has been right before,
> *so I can* calibrate my own confidence in it.

**Currently solved by:** nothing. Every tool asserts; none scores itself.
**Success:** the user acts on the number because the track record justifies it.
**Served by:** Calibration & Scoring — the differentiator.

### JTBD-6 — "Keep my financial truth in one place, forever"
> *When* I need to know what actually happened and when I learned it,
> *I want to* one immutable, auditable, portable record,
> *so I can* trust the history and never be locked in.

**Currently solved by:** scattered statements, spreadsheets, broker portals.
**Success:** one query answers any historical question; one command exports everything.
**Served by:** Bitemporal ledger, data portability.

### JTBD-7 — "Help me stay consistent without nagging me"
> *When* my discipline slips,
> *I want to* see it reflected honestly in a process metric,
> *so I can* correct course without being manipulated into it.

**Currently solved by:** willpower.
**Success:** consistency improves without any urgency mechanic existing.
**Served by:** Progression (process-only), Discipline Index.

---

## 3. Jobs Atlas explicitly does not do

| Non-job | Why | Where to go |
|---|---|---|
| "Tell me what to buy" | NG-04, regulated; Law 14 | A licensed adviser |
| "Move my money" | NG-03 | The bank or broker |
| "Help me budget by category" | NG-01 — wrong variable at this horizon | A budgeting app |
| "Show me how I compare to others" | NG-05 | Nowhere useful |
| "Make me feel good about my portfolio today" | Law 2 — that feeling is noise | — |
| "File my taxes" | Out of scope; liability | An accountant, using Atlas's worksheet |

---

## 4. The anti-persona

Atlas is explicitly **not** built for:

- Someone who wants to be told what to do without understanding why.
- An active trader — the product refuses to serve short-horizon decisions on principle.
- Someone seeking motivation through gamified excitement — the mechanics are deliberately dull.
- Someone unwilling to maintain data quality — the system is honest about degraded input rather
  than papering over it.

Naming the anti-persona matters as much as the persona: it is what makes it possible to say no to
features that would serve them.

---

## 5. Usage model

| Interaction | Frequency | Duration | Surface |
|---|---|---|---|
| Quick expense capture | A few times per week | < 10 s | Mobile |
| Glance at Mission Control | Weekly | < 60 s | Mobile |
| Read a change event | ~2–4 per month | 1–2 min | Mobile |
| Review options | Quarterly, or on a decision | 15–30 min | Large screen |
| Incident response | Rare | 30–60 min | Large screen |
| Deep analysis | Quarterly | 1–2 h | Large screen |
| Tax year review | Annual | 2–4 h | Large screen |

**Target steady state: under 30 minutes per month of routine interaction.** This is the operational
expression of Law 10 — and note that it is a *ceiling*, not a target to grow. If usage climbs, the
product is failing, not succeeding.

---

**See also:** [Vision](../00-foundation/01-vision.md) · [Dashboard Strategy](05-dashboard-strategy.md) · [Non-Goals](../00-foundation/04-non-goals.md)
