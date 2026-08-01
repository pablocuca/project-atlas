# Calibration & Scoring

**Status:** Ratified · **Owner:** CTO / AI Architect · **Context:** C4 (Core)

> **The killer feature nobody ships.** If a system says 83% and never scores itself, it is astrology
> with a dark theme. This subsystem is how Atlas earns trust empirically rather than asserting it.

---

## 1. Why this exists

Every consumer financial planning tool quotes probabilities. **None of them tell you how often they
were right.** That is not an oversight — it is because scoring yourself requires immutable,
retrievable historical forecasts (ADR-0006), a discipline almost nobody adopts, and it exposes you
to being measurably wrong.

Atlas adopts it because:

1. **A probability from an unscored model is not information.** It is a number with the shape of
   information.
2. **It is the only defence against confident wrongness.** Model bugs, bad assumptions, and regime
   changes all show up as calibration drift long before they show up as visible nonsense.
3. **It creates a moat that time alone can build.** A competitor can copy the UI in a month; they
   cannot copy ten years of scored forecasts.
4. **It gives the system a principled way to be humble.** Degraded reliability suppresses advice
   (Seam C) — which is a graceful failure mode no prescriptive system can offer.

---

## 2. What is scored

Every forecast is registered at emission with its resolvable claims (BR-700):

| Claim type | Resolution | Maturity |
|---|---|---|
| `P(portfolio ≥ X by date D)` | Compare realised value at D | Short — 1 month to 5 years |
| `P(annual savings ≥ S)` | Compare realised savings for the year | 1 year |
| `P(spending within band)` | Compare realised spending | 1 quarter–1 year |
| `P(goal G achieved by D)` | Goal state at D | Per goal |
| `Distribution of 12m portfolio return` | Realised return → PIT value | 1 year |
| `P(FI by target date)` | **Resolves once, in ~15 years** | The North Star's own claim |

### The horizon problem, and its solution
The North Star claim resolves once, decades away. Waiting for it is useless. So calibration is
built on a **ladder of short-horizon claims that share the same machinery**:

- 1-month, 3-month, 1-year, 3-year, 5-year portfolio and cashflow distributions all come from the
  *same* return model, spending model, and tax engine as the FI projection.
- If the 1-year distributions are well calibrated across hundreds of observations, the model's
  short-horizon behaviour is verified.
- Long-horizon claims remain **extrapolations** — and Atlas says so explicitly, reporting long
  horizons as `Unverified` rather than borrowing the short-horizon score.

This is the intellectually honest position: *we can prove the engine is calibrated at 1–5 years; we
cannot yet prove it at 20, and we will not pretend otherwise.*

---

## 3. Scoring rules

### 3.1 Brier score — binary claims
```
BS = (1/N) Σ (fᵢ − oᵢ)²        f = forecast probability, o ∈ {0,1}
```
Decomposed into **reliability** (calibration), **resolution** (discrimination), and **uncertainty**
(base rate). Reliability alone is insufficient — a forecast that always says the base rate is
perfectly calibrated and completely useless. Both are reported.

### 3.2 CRPS — distributional claims
```
CRPS(F, y) = ∫ (F(x) − 1{x ≥ y})² dx
```
Proper scoring rule for full distributions. Rewards being both calibrated **and sharp**, which is
exactly the pair of properties that matters.

### 3.3 PIT histogram — distributional shape
```
PITᵢ = Fᵢ(yᵢ)
```
Under a correct model, PIT values are uniform. The shape of the deviation diagnoses the defect:

| PIT shape | Diagnosis |
|---|---|
| U-shaped | Under-dispersed — **overconfident**, intervals too narrow |
| Hump-shaped | Over-dispersed — under-confident, intervals too wide |
| Skewed | Biased mean |
| Uniform | Well calibrated |

The U-shape is the failure to watch for: it is what i.i.d. lognormal models produce, and it is the
empirical test of whether [ADR-0007](../03-architecture/adr/ADR-0007-return-model.md) was the right
call.

### 3.4 Reliability diagram
Forecast probability (binned) vs observed frequency. The diagonal is perfect calibration. This is
the single artifact that most directly answers *"when Atlas says 85%, how often is it right?"* —
and it is what the user sees.

---

## 4. Per-band scoring

Calibration is computed **per horizon band and per metric** (BR-702), never as one global number:

| Band | Sample after 5 years | Status |
|---|---|---|
| 1 month | ~60 | Scored |
| 3 months | ~20 | Scored |
| 1 year | ~5 | Thin |
| 3 years | ~2 | Insufficient |
| 5 years | 0–1 | Insufficient |
| 10 years+ | 0 | **Unverified — extrapolation** |

Aggregating these into one figure would let good short-horizon scores launder unverified long-horizon
claims. Insufficient sample reports **`Unknown`, never a default-good value** (BR-705) — a small rule
that carries a lot of integrity.

---

## 5. Reliability status and the advice gate

```
ReliabilityStatus per (metric, horizonBand)
  ├─ Verified      sufficient sample, calibration within tolerance
  ├─ Degraded      sufficient sample, calibration outside tolerance
  ├─ Unknown       insufficient sample
  └─ Unverified    no resolvable claims at this horizon yet (structural)
```

**Gating (Seam C, BR-606):**

| Status | Effect on Advisory |
|---|---|
| `Verified` | Options presented normally |
| `Degraded` | **Options depending on this band are suppressed, with the reason stated** |
| `Unknown` | Options presented with an explicit low-confidence caveat |
| `Unverified` | Options presented with the extrapolation stated plainly |

This is the only relationship in the system with veto power over another context, and it is
deliberate: **the system must be able to decide it is not currently trustworthy enough to advise.**

---

## 6. Drift detection

```
Rolling calibration over a trailing window
  ├─ CUSUM on calibration error → structural break detection
  ├─ Sample-size-aware — noise in a small sample is not drift
  └─ Per band, per metric
```

On detected drift: **raise a model review, never auto-retune** (BR-703).

Automatic self-modification is banned outright. A system that silently adjusts its own model in
response to outcomes:
- destroys reproducibility of the very history used to judge it,
- can overfit to a single regime,
- and removes the human judgement that should decide whether the *world* changed or the *model* is
  wrong.

Drift opens an issue with the evidence attached. A human decides, and the decision becomes an ADR
and a new `ModelVersion`.

---

## 7. What the user sees

**One number, one diagram, on demand.**

```
Forecast Reliability            Verified · 1m–1y bands
When Atlas said 80–90%, it was right 84% of the time (n=63).
1y–5y: insufficient sample.  >5y: extrapolation, unverified.
```

Plus the reliability diagram behind one interaction. That is the entire user-facing surface of a
subsystem that runs forever — and per the Mission doc, the North Star may never be displayed without
this being within one interaction of it.

---

## 8. Bootstrapping: the first two years

Calibration has no data at launch. Three mechanisms carry it until real observations accumulate:

1. **Back-testing on historical data.** Run the model against historical Brazilian market periods
   with known outcomes. Not the same as live calibration — the outcomes are in the training data —
   but it catches gross defects. Reported separately and labelled as back-test.
2. **Synthetic calibration.** Generate data from a known process, verify the engine recovers the
   correct probabilities. This tests the *machinery*, not the *model*.
3. **Honest reporting of absence.** Until real samples exist, reliability reports `Unknown` and every
   probability carries the caveat. **Not a hidden state — a displayed one.**

Point 3 is the important one. The temptation to show a flattering placeholder score at launch would
poison the subsystem's entire purpose on day one.

---

## 9. Anti-goals

| Never | Why |
|---|---|
| Aggregate all bands into one flattering number | Launders unverified long-horizon claims |
| Auto-retune on drift | Destroys reproducibility and human judgement |
| Hide poor calibration | The subsystem's entire value is honesty |
| Score against revised history | Must score against what was *known and stated* at the time |
| Present back-test scores as live calibration | Different epistemic status; must be labelled |
| Use calibration to justify overconfidence | Good calibration at 1 year says nothing about 20 |

---

**See also:** [Forecast Engine](02-forecast-engine.md) · [Recommendation Engine](05-recommendation-engine.md) · [ADR-0006](../03-architecture/adr/ADR-0006-immutable-forecast-artifacts.md)
