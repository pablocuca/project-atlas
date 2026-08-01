# Gamification Strategy

**Status:** Ratified · **Owner:** Product / UX Architect

> Professional, engineering-flavoured progression — bounded by hard constraints that make it
> impossible for the system to reward luck or punish noise.

---

## 1. The two failure modes this design exists to prevent

### Failure mode 1 — Goodhart's law
> *"When a measure becomes a target, it ceases to be a good measure."*

Reward contribution *frequency* and the user optimises frequency: twelve small transfers instead of
one right-sized one, contributions timed to preserve a streak rather than to match cashflow. The
metric improves; the outcome does not.

**Countermeasure:** progression rewards only behaviours that are *causally linked to FI* and
*fully under user control*, and rewards are **capped per period** so gaming produces no additional
benefit.

### Failure mode 2 — punishing noise
A score that moves with portfolio value drops when markets drop. The user is punished for something
they did not do and cannot control. Worse, they learn that the scoring system is arbitrary — and
that lesson generalises to the Health Score and the North Star, contaminating the credibility of
everything.

**Countermeasure:** [ADR-0016](../03-architecture/adr/ADR-0016-no-outcome-gamification.md) and MR-8.
The Progression module's compile-time dependency set **excludes** Valuation, Forecast, and Market
Data. It cannot observe returns even by accident. The temptation is removed rather than resisted.

---

## 2. The eligible event allow-list

Progression may consume **only** these process events (INV-161, BR-800):

| Event | Under user control? | Causally linked to FI? |
|---|---|---|
| `ContributionMade` matching declared policy | ✅ | ✅ Direct |
| `ExpenseCategorised` | ✅ | ✅ Via data quality → forecast accuracy |
| `SourceReconciled` | ✅ | ✅ Same |
| `PolicyDeclared` / reviewed | ✅ | ✅ Intentionality |
| `RunbookExecuted` | ✅ | ✅ Incident response |
| `PostmortemPublished` | ✅ | ✅ Systemic improvement |
| `OptionReviewed` (accepted or dismissed with reason) | ✅ | ✅ Deliberate decision-making |
| `GoalReviewed` | ✅ | ✅ |
| `SpendingClassificationConfirmed` | ✅ | ✅ Accuracy of the FI floor |
| `TaxYearReviewed` | ✅ | ✅ |

**Anything not on this list awards nothing.** The list is data, and adding to it requires
demonstrating both properties.

---

## 3. The mechanics

### Discipline Index (0–100)
Adherence of realised behaviour to the user's **own declared policy** — never to a system-preferred
behaviour (BR-805).

```
DisciplineIndex = weighted mean of:
  ContributionAdherence      actual ÷ declared contributions
  RebalanceAdherence         band breaches addressed within the declared window
  ReviewCadence              policy and goal reviews at the declared frequency
  DataHygiene                categorisation and reconciliation currency
```

If the user declares a 20% savings rate and achieves 20%, the index is high — **even if 40% would be
better for their FI date**. That is deliberate. The index measures integrity between intention and
action; whether the intention is ambitious enough is the Advisory engine's business, not the
scoring system's.

### Contribution Streak
Consecutive periods meeting the declared contribution commitment. Breaks only through user action
or inaction (BR-802, INV-162).

**Explicit protections:**
- A market crash cannot break a streak.
- A deliberate, recorded pause (job loss, planned sabbatical) **suspends** rather than breaks it.
- **No countdown, no expiry warning, no "don't lose your streak" notification** (BR-803, Law 10).
  A streak is a record of what happened, never a lever to make you open the app.

### Operational XP
Awarded per allow-listed process action, **capped per period** so that repetition beyond a
reasonable cadence earns nothing. XP is cosmetic and confers no capability.

### Architecture Level
Reflects the **structural maturity** of the financial setup — the engineering-flavoured mechanic
that fits this user best:

| Level | Name | Criteria |
|---|---|---|
| 1 | Bootstrap | Ledger established, one source reconciled |
| 2 | Instrumented | Coverage ≥ 80%, categorisation ≥ 70% |
| 3 | Observable | All SLIs defined, emergency fund ≥ 3 months |
| 4 | Policy-driven | IPS declared, allocation bands set, contribution schedule automated |
| 5 | Tax-aware | Wrapper strategy declared, withdrawal sequence defined, exemptions tracked |
| 6 | Resilient | Emergency ≥ 6 months, income concentration < 80%, tail scenarios reviewed |
| 7 | Self-correcting | Incidents runbooked and postmortemed, forecast reliability verified |
| 8 | Autonomous | All SLOs green for 12 consecutive months |

Levels reflect **structure**, not wealth. A modest portfolio with excellent structure ranks above a
large one with none — which is exactly the right incentive, and the inverse of what a net-worth-based
system would teach.

### Velocity metrics (informational, not scored)
`SavingsVelocity` (change in real savings capacity) and `FreedomVelocity` (FI-days gained per month,
**Controllable drivers only**). Displayed as trends; never gamified, never streaked.

---

## 4. Hard constraints

| # | Constraint | Enforcement |
|---|---|---|
| G1 | Progression cannot observe returns, valuations, or market data | **MR-8, compile-time** |
| G2 | No mechanic may create urgency to open the app | Review (Law 10) |
| G3 | Streaks break only by user action or inaction | INV-162, test |
| G4 | Rewards are capped per period | Runtime |
| G5 | Progression is cosmetic — removing the module changes no financial output | **`no-frills build` CI job** (BR-804) |
| G6 | No comparison to any other person, ever | NG-05 |
| G7 | No mechanic may discourage recording bad news | Review — see §5 |
| G8 | The Discipline Index measures the user's own policy, not a system ideal | BR-805 |

### G5 in practice
A CI job compiles and runs the entire financial test suite with `Progression` and `Narrative`
removed. If anything fails, the modules are not peripheral and the build breaks. This is the
cleanest possible proof that gamification is decoration over a sound system rather than load-bearing
structure.

---

## 5. The perverse-incentive audit

Every proposed mechanic must pass this checklist before implementation. It is the most useful part
of this document.

| Question | If yes |
|---|---|
| Could a user improve this metric without improving their financial position? | **Reject** |
| Could this metric worsen due to something outside the user's control? | **Reject** |
| Does this create a reason to open the app that isn't a real need? | **Reject** |
| Could this discourage recording an unpleasant fact? | **Reject — most dangerous** |
| Could this encourage a financially suboptimal action to preserve a score? | **Reject** |
| Does this reward luck? | **Reject** |

**The fourth question is the most dangerous and the least obvious.** A mechanic that makes the user
reluctant to record a large discretionary expense — because it will break something — corrupts the
ledger, which corrupts the twin, which corrupts every forecast above it. Any mechanic that makes
honest recording feel costly is disqualified outright, no matter how motivating it otherwise is.

---

## 6. Presentation

Progression lives on its **own surface**, never on Mission Control. Rationale: Mission Control
answers "am I on track?", and a streak counter there would compete for attention with the North Star
while being far less important.

Visual language: GitHub contribution graph, Azure DevOps, engineering dashboards. Monospace,
restrained, no colour celebration, no animation, no confetti, no badges with cartoon iconography.

Notifications from Progression: **none.** Level changes and streak milestones appear when the user
visits the surface. There is no push, ever (G2).

---

## 7. What is deliberately excluded

| Excluded | Why |
|---|---|
| Net worth milestones | Rewards luck and market timing; ADR-0016 |
| Return-based achievements | Encourages performance-chasing, the most destructive retail behaviour |
| Leaderboards, social sharing | NG-05 |
| Daily login rewards | Pure engagement mechanic; NG-11 |
| Streak-loss warnings | G2, Law 10 |
| Loss-framed messaging ("you're falling behind") | Behaviourally manipulative; the SLO panel states facts instead |
| Badges for using features | Rewards the product, not the user |

---

**See also:** [ADR-0016](../03-architecture/adr/ADR-0016-no-outcome-gamification.md) · [Financial Reliability Model](../04-engines/08-financial-reliability-model.md) · [Product Philosophy Law 11](../00-foundation/03-product-philosophy.md)
