# ADR-0007 — Block bootstrap + regime switching over i.i.d. lognormal Monte Carlo

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, AI Architect

## Context

Nearly every retirement calculator in existence simulates returns as independent, identically
distributed lognormal draws. It is easy, fast, analytically convenient — and it is **wrong in the
specific way that matters most** for financial independence:

1. It assumes independence, so it cannot produce a bad *decade*, only bad *years*. Real ruin comes
   from sequences, not averages.
2. Its tails are far too thin. Empirical equity returns are leptokurtic; crises are not 6σ events.
3. It ignores regimes: volatility clusters, and inflation and returns co-move in ways that matter
   enormously to a real (deflated) FI target.
4. It ignores cross-asset correlation *changing* in crises, precisely when diversification is being
   relied upon.

For a system whose central claim is a probability, using a model that systematically understates
sequence-of-returns risk is a correctness defect, not a modelling preference.

## Decision

The primary return-generating process is an **ensemble** of:

1. **Stationary block bootstrap** over historical Brazilian and global asset returns (block length
   drawn geometrically, mean ~24 months) — preserves autocorrelation, volatility clustering, and
   realistic joint drawdowns without assuming a parametric form.
2. **Regime-switching model** (two- or three-state Markov: expansion / stress / stagflation) with
   regime-conditional means, volatilities, and correlations — including the inflation/rates block,
   which matters more in BRL than in USD.

Forecasts report the **ensemble distribution and its spread**, and the spread across model families
is reported as `modelUncertainty` (INV-122). i.i.d. lognormal is retained **only** as a documented
baseline for comparison, never as a primary engine (BR-304).

## Rationale

- Block bootstrap makes no distributional assumption while preserving the temporal structure that
  causes ruin. It is the most honest available model given limited data.
- Regime switching supplies scenarios the historical sample may under-represent, and lets the user
  reason about named futures ("what if we get a stagflation decade?").
- Disagreement *between* model families is the most credible available estimate of model
  uncertainty — and reporting it is what turns "83%" from a claim into a measurement.
- Brazilian data has short, structurally-broken history (Real Plan, hyperinflation before it), so a
  purely historical bootstrap on local data is insufficient. The ensemble hedges this explicitly.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| i.i.d. lognormal Monte Carlo | Simple; fast; industry standard | Understates sequence risk and tails; the specific errors that matter here | Wrong in the load-bearing way |
| Historical sequence replay only | Real sequences; intuitive | Tiny effective sample; Brazilian history has structural breaks | Insufficient data, especially locally |
| GARCH family | Models volatility clustering well | Single-asset focus; parameter instability; poor multi-asset joint behaviour | Doesn't solve the cross-asset problem |
| Full macro-structural model | Theoretically satisfying | Massive parameter surface; unfalsifiable at this data volume; overfits | Complexity without calibratable benefit |
| Vendor capital-market assumptions | Professional; maintained | Opaque; unversionable; unreplayable; violates AI-2 | Cannot be replayed or scored |

## Consequences

### Positive
- Sequence-of-returns risk — the dominant FI risk — is modelled rather than assumed away.
- Model uncertainty becomes a reported quantity instead of an unstated assumption.
- Calibration has a real chance of showing good scores, because the model is not structurally
  overconfident.

### Negative — accepted costs
- Substantially more compute: ensembles multiply path counts. This is why `atlas-sim` is a separate,
  scale-to-zero Job.
- More parameters to document, version, and defend. Mitigated by `AssumptionSet` hashing.
- Harder to explain to a lay audience. Mitigated by the Narrative layer and by never requiring the
  user to understand the model to use the output.

## Reversal cost

**Low.** Return models sit behind an `IReturnModel` interface and are selected by `ModelVersion`.
Adding, replacing, or retiring one is additive; historical artifacts remain valid under their own
version.

## Compliance

BR-304, BR-309, INV-122. `AssumptionSet` is hashed into the artifact identity. Calibration
(ADR-0006, C4) empirically tests whether this decision was correct — which is the point.

## References
[Forecast Engine](../../04-engines/02-forecast-engine.md) · [Calibration & Scoring](../../04-engines/06-calibration-and-scoring.md)
