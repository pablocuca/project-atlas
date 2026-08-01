# Dashboard Strategy

**Status:** Ratified · **Owner:** Product / UX Architect

> **Law 1: every surface answers a question.** This document turns that law into a buildable
> specification.

---

## 1. The question-per-card doctrine

Every card carries a `question` field in its definition. It is not documentation — it is **data**,
displayed to the user on demand, and validated at build time.

```
CardDefinition
  id, question,               // "Can I survive losing my job right now?"
  answer,                     // the computed value + unit
  interpretation,             // what this value means, plain language
  factRefs[],                 // provenance — every number traceable (Law 5)
  freshness, degradations[],  // honesty about data quality
  drillTarget                 // where "why?" leads
```

**Build-time validation:** a card with no `question`, or with a question answerable by "a number",
fails the build. `"What is my portfolio worth?"` is rejected — it is a lookup, not a question about
the user's position.

### The transformation, applied

| ❌ Rejected | ✅ Accepted | Question answered |
|---|---|---|
| Portfolio · R$ 560.000 | Emergency coverage · **8.2 months** | How long can I survive without income? |
| Today's return · +1.2% | Controllable drift (12m) · **−94 FI-days** | Am I actually getting better? |
| Dividends · R$ 1.840 | Freedom Ratio · **11%** | What share of my essential spending is already covered? |
| Asset allocation pie | Allocation drift · **+6.1pp equities, 52 days out of band** | Is my structure where I said it should be? |
| Net worth chart | P(FI by 2041) · **83% (band 76–88%)** | Am I on track? |
| Monthly spending | Discretionary budget · **40% burned, 33% elapsed, 1.21× burn** | Am I spending faster than the plan tolerates? |

Note what happened in each row: the rejected version reports a **state**; the accepted version
reports a **position relative to a target**, in a unit the user can act on.

---

## 2. Information architecture

Four surfaces, in strict priority order. Anything that does not belong on one of them does not
belong in the product.

```
┌──────────────────────────────────────────────────────────────────────┐
│  ① MISSION CONTROL — "Am I on track?"                                │
│     One screen. Answers the North Star question and nothing else.    │
├──────────────────────────────────────────────────────────────────────┤
│  ② CHANGE FEED — "What changed, and why?"                            │
│     Gated events only. Usually empty. Emptiness is success.          │
├──────────────────────────────────────────────────────────────────────┤
│  ③ OPERATIONS — "What deserves attention?"                           │
│     SLO panel, incidents, runbooks, ranked options.                  │
├──────────────────────────────────────────────────────────────────────┤
│  ④ INSTRUMENTS — "Let me look deeper."                               │
│     Ledger, positions, scenarios, calibration, tax. On demand only.  │
└──────────────────────────────────────────────────────────────────────┘
```

### ① Mission Control

The primary screen. **Six cards, maximum.** Adding a seventh requires removing one — a hard
constraint that forces every addition to justify itself against an incumbent.

```
┌────────────────────────────────┬────────────────────────────────┐
│ P(FI BY 2041)                  │ FI DATE BAND                   │
│                                │                                │
│         83%                    │  P10 ──── P50 ──── P90         │
│    band 76 – 88%               │  2038   2041    2046           │
│                                │                                │
│ Reliability: Verified (1m–1y)  │ ▲ 41 days earlier since Jan    │
│ "Am I on track?"               │ "When, realistically?"         │
├────────────────────────────────┼────────────────────────────────┤
│ HEALTH SCORE                   │ FREEDOM RATIO                  │
│                                │                                │
│         74 / 100               │         11%                    │
│  ▁▃▅ Resilience    18/25       │  R$ 1.840 of R$ 16.400/mo      │
│  ▁▃▅ Capital       21/25       │  essential spending covered    │
│  ▁▂▃ Structure     12/20       │  by non-labour income          │
│  ▁▅▅ Tax           13/15       │                                │
│  ▁▅▅ Data           8/10       │  ▲ 1.4pp over 12 months        │
│  ▁▅▅ Forecast       2/5        │                                │
│ "Which parts are outside       │ "How much freedom do I already │
│  their envelope?"              │  own?"                         │
├────────────────────────────────┼────────────────────────────────┤
│ CONTROLLABLE DRIFT (12M)       │ ATTENTION                      │
│                                │                                │
│      −94 FI-days               │  1 incident · SEV-3            │
│  gained through decisions      │  Allocation drift, 52 days     │
│  Market contribution excluded  │                                │
│                                │  3 options available           │
│  "Am I actually improving?"    │  "What deserves attention?"    │
└────────────────────────────────┴────────────────────────────────┘
```

**Why these six.** Together they answer: *am I on track (1), when (2), what's broken (3), how far
have I come in reality (4), is that because of me (5), what needs me now (6).* Every other number
in the system is reachable from these, and no other number earns a permanent place.

**Controllable Drift is the card no competitor ships**, and arguably the most valuable. Over 12
months, stochastic contributions largely cancel and decisions accumulate — so it is the only honest
answer to "am I getting better?".

### ② Change Feed

```
┌──────────────────────────────────────────────────────────────────┐
│  CHANGE FEED                                    Last 30 days     │
├──────────────────────────────────────────────────────────────────┤
│  ● 12 Jul · Salary increase                          −28 FI-days │
│    Controllable · confidence high                                │
│    Your primary income rose 8.4%. At your current savings rate,  │
│    this brings the median FI date forward by 28 days.            │
│    → 3 options available to amplify this                         │
├──────────────────────────────────────────────────────────────────┤
│  ● 03 Jul · SELIC path revision                       −7 FI-days │
│    Structural · confidence medium                                │
│    Market expectations for the rate path shifted. Your fixed     │
│    income allocation benefits; your mortgage does not.           │
├──────────────────────────────────────────────────────────────────┤
│  ⌄ 14 suppressed movements (market variance)          ±31 days   │
│    Not shown because they are not attributable to any decision   │
│    or structural change. Tap to inspect.                         │
└──────────────────────────────────────────────────────────────────┘
```

**The suppressed row is the most important element on this screen.** It makes the Signal Gate
*visible* — the user can always see that Atlas is filtering, how much it filtered, and can inspect
it. Filtering without disclosure would be paternalism; filtering with disclosure is a service.

An empty Change Feed is a **success state**, and must be presented as such:

> *No attributable changes in the last 30 days. Your trajectory is stable.*

Never as an empty-state apology, never with a prompt to do something.

### ③ Operations

SLO panel (each SLI with target, current, error budget consumed, burn rate) · open incidents with
runbooks · ranked options · calibration status. Grafana-like density is appropriate here — this is
the surface for the user *as engineer*.

### ④ Instruments

Ledger explorer with bitemporal as-of controls · position book with lots and basis · scenario
comparison · calibration diagrams · tax year view with rule traces · variance inspector.

---

## 3. Universal display rules

| Rule | Detail |
|---|---|
| **No unlabelled probability** | Every probability shows its band and its reliability status |
| **No point estimate for a distribution** | FI date always shows P10/P50/P90 |
| **No unattributed delta** | Any Δ shown carries its driver class |
| **Freshness always available** | Every card can state its data age within one interaction |
| **Degradation is visible** | Degraded data is labelled on the card, not buried in settings |
| **Provenance in one tap** | Any number → the artifact and inputs that produced it |
| **Real terms by default** | Nominal figures explicitly labelled (BR-307) |
| **After tax by default** | Pre-tax figures explicitly labelled (Law 12) |
| **No red/green for market movement** | Colour is reserved for SLO status; market movement is neutral grey |

The last rule is small and load-bearing. Red/green on market movement is the visual grammar that
teaches users to feel good or bad about noise. Atlas reserves that grammar for things that are
genuinely good or bad — SLO status, budget burn, incident severity.

---

## 4. Colour and typography semantics

| Signal | Colour | Used for |
|---|---|---|
| Nominal / within envelope | Neutral | Default state of everything |
| Attention | Amber | SLO burn > 1×, degraded data, expiring option |
| Breach | Red | SLO breach, open incident, ruin probability above tolerance |
| Improvement (controllable) | Blue, not green | Controllable improvement — deliberately *not* the colour of market gain |
| Stochastic | Grey, always | Market movement, in every context, permanently |

**Stochastic is always grey.** Not red, not green, not amber — the visual language must never
suggest that market movement is good news or bad news. It is neither; it is variance.

Typography: monospace for all figures (aligned comparison), tabular numerals, unit always adjacent
to the value, never in a legend.

---

## 5. What is deliberately absent

| Absent | Reason |
|---|---|
| A "total net worth" hero number | An outcome, not controllable. NG-02 |
| Daily P&L anywhere | Law 2 |
| Ticker lists, top movers, watchlists | NG-02 |
| Streak counters on Mission Control | Law 10 — no urgency to open the app |
| Notification badges for non-incidents | Law 9 |
| Onboarding checklists, progress-to-100% setup meters | Engagement mechanics, NG-11 |
| Peer or benchmark comparison | NG-05 |
| Any celebration of portfolio milestones | ADR-0016 |

---

## 6. Mobile vs large screen

| Surface | Mobile (primary) | Large screen (secondary) |
|---|---|---|
| Mission Control | 6 cards, vertical, one per viewport height | 2×3 grid, dense |
| Change Feed | Full parity | Full parity + filtering |
| Operations | SLO summary + incidents | Full Grafana-density panel |
| Instruments | Read-only summaries | Full exploration, tables, scenario compare |
| Quick capture | **Primary use case** — record an expense in < 10 s | Available |

Mobile optimises for **capture and glance**. Large screen optimises for **analysis**. Quick capture
on mobile is not a convenience feature — it is what keeps the ledger current, which is what keeps
the Coverage and Freshness SLIs green, which is what keeps every number above them trustworthy.

---

**See also:** [UX Architecture](07-ux-architecture.md) · [Attribution Engine](../04-engines/04-attribution-engine.md) · [Product Philosophy](../00-foundation/03-product-philosophy.md)
