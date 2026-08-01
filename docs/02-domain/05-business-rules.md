# Business Rules

**Status:** Ratified · **Owner:** Domain · **Last reviewed:** 2026-08-01

Numbered, testable rules. `BR-nnn` identifiers are permanent and cited in test names
(`BR_210_StochasticDriversNeverAlert`). A rule with no test is a wish, not a rule.

**Enforcement column:** `Type` = impossible to express illegally · `Runtime` = invariant check ·
`Test` = property/golden test · `Review` = human gate.

---

## BR-0xx — Money and arithmetic

| ID | Rule | Enforcement |
|---|---|---|
| BR-001 | Monetary values are integer minor units with a commodity. No `double`/`float` in any financial path | Type + analyser |
| BR-002 | Arithmetic across commodities is illegal without an explicit `MarkSet` and instant | Type |
| BR-003 | Rounding is banker's (half-even), applied exactly once, at a declared presentation or settlement boundary | Runtime + Test |
| BR-004 | Splitting an amount must re-sum to the original; remainders are allocated by largest-remainder | Test (property) |
| BR-005 | Percentages are stored as exact decimals, never as floats, and never as pre-multiplied basis points without a unit type | Type |
| BR-006 | Any value crossing a context boundary carries its unit. Bare numbers are forbidden in contracts | Review + contract test |

## BR-1xx — Ledger

| ID | Rule | Enforcement |
|---|---|---|
| BR-100 | Every journal entry balances to zero **per commodity** | Runtime |
| BR-101 | Posted entries are immutable. Corrections are reversal + replacement, both linked to the original | Type + Runtime |
| BR-102 | Every entry carries `ValidTime` and `DecisionTime`; balance queries require both | Type |
| BR-103 | Re-importing identical source data never creates a duplicate entry (`idempotencyKey` unique per source) | Runtime + Test |
| BR-104 | `ValidTime` may not exceed the current trading day's close | Runtime |
| BR-105 | An account's type is immutable after its first posting | Runtime |
| BR-106 | No account may be closed with a non-zero current balance | Runtime |
| BR-107 | Any ledger state must be reconstructible by replaying entries in `DecisionTime` order | Test (golden) |
| BR-108 | A source reconciliation discrepancy over tolerance raises a data-quality SLI breach, never a silent adjustment | Runtime |
| BR-109 | Manual entry is a first-class source with the same invariants as automated ingestion — no privileged bypass | Type |

## BR-2xx — Change, signal, attribution *(the anti-noise core)*

| ID | Rule | Enforcement |
|---|---|---|
| BR-200 | No delta is displayed as a change before it is attributed | Type (display accepts `ChangeEvent` only) |
| BR-201 | Attribution contributions plus residual equal the total delta exactly | Runtime |
| BR-202 | Unexplained residual > 2% of total delta rejects the attribution and raises an engineering alert | Runtime |
| BR-203 | Deltas are attributable only between artifacts of the **same** `ModelVersion` | Type |
| BR-204 | A change caused by a model upgrade is labelled `ModelChange` and shown in a separate channel from life changes | Runtime + Review |
| BR-210 | **Stochastic drivers never produce alerts, notifications, or headline changes** | Type (gate input excludes them) |
| BR-211 | A delta becomes a `ChangeEvent` only if it passes materiality **and** significance **and** deduplication (INV-134) | Runtime |
| BR-212 | Default materiality: ≥ 30 FI-days **or** ≥ 1.0pp `P(FI by target)` | Config + Test |
| BR-213 | Default significance: > 2σ of the trailing 90-day stochastic distribution for that metric | Config + Test |
| BR-214 | Suppressed deltas are retained and queryable. Nothing is deleted | Runtime |
| BR-215 | Suppression windows: same driver, same direction, within 14 days ⇒ merged, not repeated | Runtime |
| BR-216 | Market movement consumes the Variance Budget. Variance Budget exhaustion is **informational only** — it never becomes an incident | Runtime |

## BR-3xx — Forecast and simulation

| ID | Rule | Enforcement |
|---|---|---|
| BR-300 | Every forecast is computed from exactly one `TwinSnapshot` and reads nothing else | Type + Test |
| BR-301 | Forecasts are immutable, content-addressed, and never deleted | Runtime |
| BR-302 | Re-running a forecast with identical `(snapshot, model, assumptions, seed)` reproduces identical output | Test (CI determinism gate) |
| BR-303 | Every distribution reports P10/P50/P90 and model uncertainty. A bare point estimate is unconstructible | Type |
| BR-304 | Return models must be regime-aware or block-bootstrapped. i.i.d. lognormal-only models are forbidden as the primary engine | Review + ADR-0007 |
| BR-305 | Human capital carries an explicit market correlation (`marketBeta`); zero must be a deliberate, recorded choice | Type |
| BR-306 | All projections are after-tax, routed through `ITaxJurisdiction` | Test |
| BR-307 | Projections are in real (deflated) terms by default; nominal figures must be explicitly labelled | Type |
| BR-308 | Commodities of kind `Unmodelled` are excluded from distributions and reported in a separate bucket | Runtime |
| BR-309 | Path count must satisfy the configured Monte Carlo standard-error target; under-converged runs are rejected | Runtime |
| BR-310 | Re-forecast is triggered by the materiality gate or the daily tick, never by every posting | Runtime |
| BR-311 | A forecast whose snapshot contains marks staler than tolerance is computed, but flagged `Degraded` | Runtime |

## BR-4xx — Taxation (Brazil)

Detailed computational rules live in [Tax Engine — Brazil](../04-engines/07-tax-engine-brazil.md).
These are the *structural* rules.

| ID | Rule | Enforcement |
|---|---|---|
| BR-400 | The tax module is pure: no I/O, no clock, no randomness. Time is always a parameter | Type + Test |
| BR-401 | Every `TaxConsequence` carries a `RuleTrace` explaining each rule applied and its inputs | Type |
| BR-402 | Every computation records the `RulesetVersion` and its effective date range | Type |
| BR-403 | Historical forecasts are replayed under the ruleset effective at their computation date, never today's | Test (golden) |
| BR-404 | Exemption eligibility is computed per period and per regime, and its consumption is tracked cumulatively | Runtime |
| BR-405 | Withholding at source is modelled explicitly and netted, never assumed away | Test |
| BR-406 | Withdrawal sequence is a first-class policy dimension and is always evaluated after-tax | Type |
| BR-407 | A rule whose legal basis is not cited in the ruleset metadata cannot be activated | Review |
| BR-408 | When the correct treatment is genuinely ambiguous, the engine computes the **conservative** (higher-tax) branch and flags `TaxAmbiguity` for user review | Runtime |

## BR-5xx — Reliability, SLIs, SLOs

| ID | Rule | Enforcement |
|---|---|---|
| BR-500 | An SLI that can be moved by market marks alone is invalid and rejected at definition time | Runtime |
| BR-501 | Every SLO's error budget is denominated in the natural unit of its SLI | Type |
| BR-502 | Two consecutive breach evaluations declare an incident with a linked runbook | Runtime |
| BR-503 | Incident resolution requires a postmortem within 7 days; failure raises a process incident | Runtime |
| BR-504 | Postmortems are blameless: the template forbids attributing cause to the person | Review |
| BR-505 | Health Score components must each be independently noise-immune; a component failing this cannot be included | Review + Test |
| BR-506 | Health Score is never presented without its component breakdown available in one interaction | Review |
| BR-507 | Burn-rate alerting uses multi-window (fast + slow) evaluation to avoid single-window flapping | Test |

## BR-6xx — Advisory and decision support

| ID | Rule | Enforcement |
|---|---|---|
| BR-600 | Options are evaluated as counterfactuals on the **same** twin snapshot as the baseline | Type |
| BR-601 | Between 2 and 5 options are presented. A single option is never presented alone | Runtime |
| BR-602 | Every option states assumptions, sensitivities, and an explicitly non-empty `notModelled` list | Runtime |
| BR-603 | Option cost is after-tax. Pre-tax option economics fail the build | Test |
| BR-604 | Options are phrased as evaluated choices, never as instructions. Banned imperatives are linted in copy | Test (copy lint) |
| BR-605 | Options naming a specific security are forbidden (NG-04); allocation options operate at asset-class level | Type |
| BR-606 | Degraded forecast reliability for a horizon suppresses options depending on it, with the reason stated | Runtime |
| BR-607 | An accepted option must create a new `Policy` version citing the `OptionId` | Runtime |
| BR-608 | Options expire; a stale option computed on an outdated snapshot may not be displayed | Runtime |
| BR-609 | Dismissed options are recorded with reason where offered, and feed option-generation preferences | Runtime |

## BR-7xx — Calibration

| ID | Rule | Enforcement |
|---|---|---|
| BR-700 | Every forecast is registered for future scoring at emission time | Runtime |
| BR-701 | Outcomes are scored against the forecast as it stood, using the ruleset and model of that date | Test |
| BR-702 | Calibration is computed per horizon band and per metric — never as one global number | Runtime |
| BR-703 | Drift detection triggers a **model review**, never an automatic retune. Silent self-modification is forbidden | Review |
| BR-704 | Reliability status gates advice (BR-606) and is displayed alongside the North Star | Runtime |
| BR-705 | Insufficient sample for a horizon band reports `Unknown`, never a default-good value | Type |

## BR-8xx — Progression (gamification)

| ID | Rule | Enforcement |
|---|---|---|
| BR-800 | Progression may consume only process events on the eligible allow-list | Type (module deps) |
| BR-801 | XP, streaks, levels, and indices may never be affected by returns, valuations, or market movement | Type |
| BR-802 | A streak breaks only through user action or inaction | Runtime |
| BR-803 | No mechanic may create urgency to open the app (no streak-loss countdowns, no expiring rewards) | Review (Law 10) |
| BR-804 | Progression state is cosmetic: removing the entire module must not change any financial output | Test (module-removal test) |
| BR-805 | The Discipline Index measures adherence to the user's *own declared policy*, never to a system-preferred behaviour | Review |

## BR-9xx — Narrative and AI

| ID | Rule | Enforcement |
|---|---|---|
| BR-900 | The narrative layer receives only a validated `FactSet` and has no access to domain internals | Type (module deps) |
| BR-901 | Every numeric token in generated prose must resolve to a `Fact.id`; unresolved ⇒ reject and fall back | Runtime |
| BR-902 | The narrative layer performs no arithmetic, including rounding and unit conversion | Type |
| BR-903 | Generated text is never persisted as a fact and is never an input to any computation | Type |
| BR-904 | Model or prompt changes require regression against the narrative golden corpus | Test |
| BR-905 | Generated content is labelled as generated, with the model and version identifiable | Runtime |
| BR-906 | Prompts never contain raw ledger detail beyond what the `FactSet` requires (data minimisation) | Review + Test |

## BR-Bxx — Localisation *(product surface only)*

Per [ADR-0023](../03-architecture/adr/ADR-0023-bilingual-product-english-spec.md). The specification,
codebase, identifiers, and telemetry are English-only; these rules govern the bilingual UI.

| ID | Rule | Enforcement |
|---|---|---|
| BR-B01 | Tier-1 regulatory terms are never translated in any locale; an English gloss accompanies first use | Terminology lint |
| BR-B02 | Tier-2 domain terms use exactly one canonical pairing per locale; a second rendering is a defect | Terminology lint |
| BR-B03 | Every user-facing message key exists in every supported locale; a missing key fails the build | ICU completeness gate |
| BR-B04 | **The copy lint runs per locale.** A locale without banned lists for every category fails the build | CI, build-failing |
| BR-B05 | User-facing text is composed as full ICU messages; string concatenation in these paths is banned | Analyser + review |
| BR-B06 | `FactSet.displayString` is locale-keyed and formatted server-side; the client performs no formatting | Type + review |
| BR-B07 | Narrative is **generated** per locale, never machine-translated after generation | Type + review |
| BR-B08 | Traceability verification (BR-901) runs against the locale-correct `displayString` | Runtime |
| BR-B09 | Currency is never converted for display; language and jurisdiction are orthogonal | Type + review |
| BR-B10 | Error codes, identifiers, log fields, span names, and export schemas are English in all locales | Analyser |
| BR-B11 | Tax ruleset `legalBasis` citations remain Portuguese in all locales | Ruleset validator |
| BR-B12 | Every string carries a `lang` attribute; Tier-1 terms inside English prose are wrapped `lang="pt-BR"` | Accessibility audit |

> **BR-B04 is compliance-critical.** The advice boundary in
> [ADR-0022](../03-architecture/adr/ADR-0022-advice-posture.md) is enforced mechanically. An
> English-only banned list would hold the boundary in English and leave it entirely unenforced in
> Portuguese — a gap that passes review because the lint still goes green.

## BR-Axx — Data lifecycle and portability

| ID | Rule | Enforcement |
|---|---|---|
| BR-A00 | A single command exports ledger, snapshots, artifacts, and calibration history to open formats | Test (CI runs the export) |
| BR-A01 | Exports are self-describing: schema and units travel with the data | Test |
| BR-A02 | Ledger entries, twin snapshots, and forecast artifacts have **no deletion path** in the application | Type |
| BR-A03 | Every snapshot and artifact schema version remains deserialisable permanently; readers are never removed | Test (historical corpus) |
| BR-A04 | Personal data deletion (LGPD) is satisfied by tenant-scoped cryptographic erasure, not row deletion | Review + ADR-0020 |
| BR-A05 | No production data leaves the tenant boundary for telemetry; metrics are aggregate and non-identifying | Review |

---

## Traceability

Every `BR-` maps to at least one automated test, listed in
[Testing Strategy §7](../05-engineering/03-testing-strategy.md). CI fails if a `BR-` identifier
exists in this document with no matching test annotation — the coverage of *rules*, not lines, is
the gate.
