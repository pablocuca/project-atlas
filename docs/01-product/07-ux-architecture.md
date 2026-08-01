# UX Architecture

**Status:** Ratified · **Owner:** UX Architect

> The product must feel like an **operating system**, not a finance app. This document specifies
> what that means concretely enough to build.

---

## 1. What "feels like an OS" actually means

The reference points — Grafana, Azure Monitor, Datadog, GitHub Insights, a flight cockpit — share
five properties. These are the design targets, stated as testable qualities:

| Property | Meaning in Atlas |
|---|---|
| **Instrumentation over decoration** | Every pixel reports state. No illustration, no mascot, no hero imagery |
| **Density with hierarchy** | High information density, but ranked — the eye finds the anomaly first |
| **Status is the primary visual language** | Nominal / attention / breach. Not good/bad, not up/down |
| **Drill-down is universal** | Any value opens into its components, then its source |
| **Quiet by default** | A healthy system shows a calm screen. Alarms mean something |

And one property the reference products have that a finance app usually lacks: **the operator is
assumed competent.** No tooltips explaining what a percentage is, no celebratory copy, no
reassurance.

---

## 2. The interaction laws

| # | Law | Consequence |
|---|---|---|
| UX-1 | Every surface answers a stated question | Cards carry their question as data |
| UX-2 | Any number is one interaction from its provenance | Tap → components → artifact → inputs |
| UX-3 | Uncertainty is always visible | Bands, not points. Reliability status adjacent to probabilities |
| UX-4 | Degradation is on the surface, not in settings | A degraded card says so on its face |
| UX-5 | Stochastic movement is visually neutral, always | Grey. Never red, never green |
| UX-6 | No surface creates urgency | No countdowns, no expiry pressure, no badges for non-incidents |
| UX-7 | Empty is a success state | "No attributable changes" is presented as good news, not an empty state |
| UX-8 | Capture is faster than analysis | Recording a fact must never require navigating a hierarchy |
| UX-9 | The system's own uncertainty is shown, not hidden | "Atlas is not confident enough to advise here" is a designed state |
| UX-10 | Nothing is irreversible without confirmation and provenance | Corrections, erasure, policy changes |

**UX-7 and UX-9 are unusual and important.** Most products treat emptiness and uncertainty as design
failures to be filled or hidden. In Atlas both are *meaningful signals*, and they get first-class
visual treatment rather than apologetic placeholders.

---

## 3. Navigation model

```
                    ┌─────────────────┐
                    │ MISSION CONTROL │  ← always the entry point
                    └────────┬────────┘
          ┌──────────────────┼──────────────────┐
          ▼                  ▼                  ▼
  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐
  │ CHANGE FEED  │   │  OPERATIONS  │   │ INSTRUMENTS  │
  │ what changed │   │ what needs me│   │ let me look  │
  └──────┬───────┘   └──────┬───────┘   └──────┬───────┘
         │                  │                  │
         ▼                  ▼                  ▼
    attribution        SLOs, incidents,    ledger, positions,
    drill-down         runbooks, options   scenarios, calibration,
                                           tax, variance inspector
                                │
                    ┌───────────▼───────────┐
                    │   ⊕ CAPTURE (global)  │  ← reachable from anywhere, one tap
                    └───────────────────────┘
```

Four destinations. Flat. No nested tab bars, no hamburger menus hiding primary function.

**Capture is global and always one tap away** (UX-8) because the ledger's currency is what keeps
every number above it trustworthy. Making capture cheap is a *data quality* decision expressed as an
interaction decision.

---

## 4. The drill-down spine

Universal, and identical everywhere:

```
VALUE
  ↓ tap
COMPONENTS          what makes up this number
  ↓ tap
ATTRIBUTION         what moved it, and which class
  ↓ tap
ARTIFACT            which forecast produced it, which model version
  ↓ tap
INPUTS              which twin snapshot, which coverage, which staleness
  ↓ tap
LEDGER              the entries themselves, bitemporally
```

Any number, anywhere, follows this path. A user who does not care never sees past level one; a user
who does can always reach the journal entry. This is the interaction expression of Law 5 —
traceability is not a debugging feature, it is the product's core trust mechanism.

---

## 5. Visual system

### Colour semantics

| Token | Use | Never used for |
|---|---|---|
| `neutral` | Default state of every value | — |
| `stochastic` (grey) | **All** market movement, permanently | Anything the user controls |
| `controllable` (blue) | Improvement attributable to a decision | Market gain |
| `attention` (amber) | Burn rate > 1×, degraded data, expiring option | Market decline |
| `breach` (red) | SLO breach, open incident, ruin above tolerance | Market decline |
| `verified` (subtle green) | Calibration verified, SLO healthy | Portfolio gain |

**Green never means "the market went up."** Green means "this is within its envelope" or "this is
verified". The visual grammar deliberately refuses to teach the user to feel good about noise.

### Typography
Monospace for all figures, tabular numerals, unit adjacent to the value never in a legend. Sans-serif
for prose. Weight and size carry hierarchy; colour carries status. Nothing carries decoration.

### Density
Mobile: one card per viewport height on Mission Control; dense lists elsewhere.
Large screen: Grafana-class density is correct and desirable — this user reads dashboards for a living.

### Motion
Functional only: state transitions, drill-down continuity, loading. **No celebratory animation, no
confetti, no counting-up numbers.** A number that animates upward is a small dishonesty about how
the value arrived.

---

## 6. Copy

| Rule | Example |
|---|---|
| State facts; never editorialise | ✅ "Savings rate 24%, target 30%, 3 months below" ❌ "You're falling behind!" |
| Name the driver, not the person | ✅ "Discretionary spend exceeded plan" ❌ "You overspent" |
| Never use imperatives for advice | ✅ "Option 2: redirect R$1.200/month…" ❌ "You should…" |
| Quantify, don't qualify | ✅ "−28 FI-days" ❌ "significant improvement" |
| Admit ignorance explicitly | ✅ "Not modelled: employer plan fees" |
| Units always attached | ✅ "8.2 months" ❌ "8.2" |
| No urgency vocabulary | Banned: "act now", "don't miss", "last chance", "hurry" |

A **copy lint** in CI enforces the banned-construction list ([Recommendation Engine §6](../04-engines/05-recommendation-engine.md)).
This is crude by design: the boundary between "evaluated option" and "instruction" is linguistic, and
language drifts under pressure to be helpful.

**The surface is bilingual (pt-BR default, en), so every rule above applies in both languages** —
and the copy lint carries banned lists per locale (BR-B04). Portuguese has traps English does not:
*"Recomendação"* carries a regulatory connotation that *"option"* does not, so `Ranked Option`
renders as **`Opção Avaliada`**, never *"Recomendação"*. Regulatory terms (`come-cotas`, `DARF`,
`isenção`) appear **untranslated in the English UI too**, with a gloss. Full rules:
[Localisation Strategy](11-localisation-strategy.md).

---

## 7. Notification design

Notifications are the product's most dangerous surface — the place where a good system becomes an
anxiety machine most easily.

| Rule | Detail |
|---|---|
| Source | **Only** `ChangeEvent` (post-Signal-Gate) and `IncidentDeclared` |
| Ceiling | ≤ 4 per month at steady state (NFR-808). Exceeding it is a **product defect** |
| Content | The change, its magnitude, its driver class, its FI-day impact. Never a teaser |
| Never | Streak reminders, re-engagement prompts, market movement, progression events, "you haven't opened Atlas in a while" |
| Timing | Batched to a daily digest window unless SEV-1/SEV-2 |
| Actionability | Every notification links to something the user can inspect or act on |

The ceiling being a *defect threshold* rather than a target is the whole point. Every other product
in this category treats notification volume as an engagement lever; Atlas treats it as a leak.

---

## 8. Accessibility

| Requirement | Target |
|---|---|
| Contrast | WCAG 2.2 AA (NFR-804) |
| Dynamic Type | All surfaces reflow; no fixed-height text containers |
| VoiceOver | Mission Control fully navigable; every card announces its question and answer |
| Colour independence | Status conveyed by icon and label as well as colour (NFR-807) |
| Motion | Respects reduce-motion; all motion is optional |
| Touch targets | ≥ 44pt |

Colour independence matters more here than usual: the entire visual language is status-coded, so a
colour-only encoding would make the product unusable rather than merely harder.

---

## 9. Onboarding

Deliberately minimal, and deliberately *not* a funnel.

```
1. Create accounts (chart of accounts) — templated, editable
2. Record or import opening balances
3. Declare essential vs discretionary for existing categories
4. Set Target Date, Confidence Target, Horizon Age
5. Declare an initial Policy
→ First forecast
```

**No progress meter, no checklist gamification, no "complete your profile" pressure** (NG-11). The
system is honest about being partially configured: coverage and reliability report low, and every
number is labelled accordingly. A partially-set-up Atlas is a *degraded* Atlas, and it says so —
which is far more useful than a nag.

---

## 10. Offline behaviour

| Capability | Offline |
|---|---|
| Capture an expense | ✅ Full, queued, synced on reconnect |
| View last Mission Control state | ✅ Cached, with an explicit staleness label |
| View Change Feed | ✅ Cached |
| Run a forecast or evaluate options | ❌ Requires the server; stated plainly |

Cached values always display their age. A stale number shown without its age is the exact failure
Law 8 exists to prevent — and offline is where that failure is easiest to make.

---

**See also:** [Dashboard Strategy](05-dashboard-strategy.md) · [Gamification Strategy](06-gamification-strategy.md) · [Product Philosophy](../00-foundation/03-product-philosophy.md)
