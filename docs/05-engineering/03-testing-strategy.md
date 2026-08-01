# Testing Strategy

**Status:** Ratified · **Owner:** CTO / Staff Engineer

> In a system whose value is the correctness of its numbers, **the test suite is the product's
> warranty.** Coverage of *rules*, not lines, is the metric that matters.

---

## 1. The test portfolio

The classic pyramid is insufficient here. Atlas needs five categories the pyramid does not name, and
those five are where the real risk lives.

```
                    ┌─────────────────────────┐
                    │  Manual exploratory      │  rare, exploratory only
                    ├─────────────────────────┤
                    │  Integration / E2E       │  ~30
                    ├─────────────────────────┤
                    │  Contract tests          │  per published language
                    ├─────────────────────────┤
                    │  Unit + property tests   │  the bulk
                    └─────────────────────────┘

     ┌───────────────── ALONGSIDE, NOT ABOVE ─────────────────┐
     │ ① Architecture tests   boundaries cannot erode          │
     │ ② Golden-file tests    tax + adapters + canonicalisation│
     │ ③ Determinism tests    AI-2 — the replay guarantee      │
     │ ④ Historical corpus    2026 data readable in 2046       │
     │ ⑤ Rule coverage        every BR- has a test             │
     └─────────────────────────────────────────────────────────┘
```

The five bracketed categories protect properties that unit tests structurally cannot: **they test
the system's shape and its promises over time**, not its behaviour at a point.

---

## 2. Property-based testing

Where the domain has algebraic laws, examples are weak and properties are strong. `FsCheck`.

| Property | Statement |
|---|---|
| Money split conservation | For any amount and divisor, the parts re-sum to the original exactly |
| Money commodity safety | Arithmetic on differing commodities always throws |
| Entry balance | Any generated valid entry sums to zero per commodity |
| Bitemporal monotonicity | Decision time never decreases; belief intervals never overlap for one fact key |
| Ledger replay | Replaying entries in decision-time order reproduces the state exactly |
| Basis conservation | Any sequence of splits and bonuses preserves total cost basis |
| Regressive table monotonicity | Longer holding never yields a higher rate |
| Exemption bounds | Cumulative exemption consumption never exceeds the statutory limit |
| Attribution efficiency | Driver contributions plus residual equal the total delta, exactly |
| Shapley symmetry | Two drivers with identical marginal contributions receive equal attribution |
| Canonicalisation stability | Semantically identical snapshots produce identical bytes |

**Basis conservation is the highest-value property in the suite.** Corporate actions are where cost
basis silently rots, and the resulting tax error is invisible until it is years old.

---

## 3. Golden-file testing

### Tax (the most important corpus in the project)
```
fixtures/golden/tax/
  disposal-acoes-above-exemption.2026.1.0.golden.json
  disposal-acoes-within-exemption.2026.1.0.golden.json
  disposal-fii-no-exemption.2026.1.0.golden.json
  come-cotas-may.2026.1.0.golden.json
  renda-fixa-early-redemption.2026.1.0.golden.json
  pgbl-withdrawal-regressive.2026.1.0.golden.json
  …
```

Rules:
- Expected outputs are **independently verified** — computed by hand or by a tax professional, never
  generated from the implementation. A golden file produced by the code under test proves nothing.
- Every ruleset version keeps its own corpus, and **all versions run on every build** (BR-403).
- A golden file includes the full `RuleTrace`, so a change in *reasoning* fails even when the total
  happens to match.

### Adapters
Real, redacted source payloads per source and per schema version. Layout drift is then caught by CI
rather than by a wrong number appearing in production months later.

### Canonicalisation
Fixed snapshots with their expected SHA-256, asserted across Linux/macOS and multiple .NET patch
versions. This is what keeps AI-2 from failing silently.

---

## 4. Determinism gate

```gherkin
Given a fixed corpus of (snapshotHash, modelVersion, assumptionSet, seed) keys
When each is re-run on this build
Then the produced artifact bytes are identical to the stored expected artifact
```

Runs on every PR. **A failure blocks release and is SEV-1.** In production, one sampled artifact per
day is re-run and compared as a live canary.

Rationale for the severity: if determinism breaks, every historical comparison in the system becomes
suspect, and the calibration record — which takes years to accumulate — is compromised. There is no
cheap recovery.

---

## 5. Historical corpus test

```gherkin
Given fixtures/corpus/ contains one snapshot and one artifact per schema version ever released
When the build runs
Then every one deserialises successfully with current code
And the resulting object graph matches its stored expectation
```

Deleting a reader breaks the build (BR-A03). This is how "readable in 2046" becomes an engineering
fact rather than an intention.

---

## 6. Architecture tests

One named test per rule, citing its identifier:

```csharp
[Fact] public void MR_01_DomainProjectsDependOnlyOnKernel()
[Fact] public void MR_07_TaxationHasNoTimeOrIoDependency()
[Fact] public void MR_08_ProgressionCannotReferenceValuationOrForecast()
[Fact] public void MR_09_NarrativeReferencesOnlyFactSetContracts()
[Fact] public void MR_10_NoCyclicModuleDependencies()
```

Plus analyser-backed checks: no `DateTime.Now` in `*.Domain`, no `double`/`float` in financial types,
no `DbContext` outside `*.Infrastructure`, no vendor telemetry SDK anywhere.

---

## 7. Rule coverage gate

```
1. Parse docs/02-domain/05-business-rules.md → set of BR- identifiers
2. Scan test assemblies for [BusinessRule("BR-nnn")] attributes
3. Fail the build on any BR- with no test
4. Report BR- identifiers with only one test as a warning
```

This is the mechanism that keeps the specification honest. A rule can be written down and forgotten;
a rule that fails the build until tested cannot be.

---

## 8. Simulation and statistical testing

Stochastic code is genuinely hard to test. Four techniques:

| Technique | What it verifies |
|---|---|
| **Analytic degenerate cases** | With zero volatility and fixed returns, output must match a closed-form calculation exactly |
| **Synthetic recovery** | Generate paths from a known process; assert the engine recovers the known probabilities within tolerance |
| **Invariant checks** | Ruin probability monotonic in spending; FI date monotonic in savings rate; no negative quantities |
| **Convergence** | Standard error decreases as `1/√N` at the expected rate |

The degenerate case is the most valuable: a Monte Carlo engine that cannot reproduce a deterministic
calculation when all randomness is removed has a bug in its *mechanics*, and that bug would otherwise
hide behind statistical noise indefinitely.

---

## 9. The `no-frills build`

```gherkin
Given Progression and Narrative are excluded from the solution
When the full financial test suite runs
Then every test passes
```

Proves BR-804: gamification and AI are decoration over a sound system. This is the cleanest available
evidence that the peripheral layers are genuinely peripheral — and it runs on every PR, so it stays
true.

---

## 10. Copy lint

Scans option text, notification templates, and narrative templates for:
- imperative advice constructions ("you should", "buy", "sell", "act now")
- urgency vocabulary ("hurry", "last chance", "don't miss")
- guarantee language ("guaranteed", "safe", "risk-free")
- security names in option text

Crude, and deliberately so: the boundary between "evaluated option" and "instruction" is linguistic,
and language drifts under pressure to be helpful. A mechanical check that fails the build outlasts
any review guideline.

**It runs per locale** (BR-B04). A locale with no banned list for any category fails the build — an
English-only list would leave the advice boundary unenforced in Portuguese while still going green.

## 10a. Localisation tests

| Test | Scope |
|---|---|
| ICU completeness | Every message key present in every locale; a missing key fails the build |
| Formatting golden files | Number, currency, percent, date, duration — per locale |
| No-concatenation lint | Bans string concatenation in user-facing text paths (Portuguese agreement) |
| Terminology lint | Tier-1 terms untranslated; Tier-2 terms match the canonical pairing exactly |
| Narrative golden corpus | **One corpus per locale**; both regress on any prompt or model change |
| Traceability per locale | `1.234,56` must resolve against its own displayString, not the English one |
| Layout snapshot | Both locales at maximum Dynamic Type — pt-BR at large text is the worst case |

---

## 11. What is deliberately not tested

| Not tested | Why |
|---|---|
| LLM output quality | Non-deterministic. **Traceability** is tested instead — the property that matters |
| Exact visual appearance | Brittle; low value. Accessibility and layout invariants are tested |
| Third-party API behaviour | Contract-tested against recorded fixtures instead |
| Performance to the millisecond | Benchmarked with generous thresholds; only regressions matter |

---

## 12. Coverage targets

| Area | Target | Note |
|---|---|---|
| **Business rules** | **100%** | The real metric. CI-gated |
| Taxation module | ≥ 95% line | Highest-risk code in the system |
| Ledger, Attribution, Forecast | ≥ 90% line | Core correctness |
| Other domain modules | ≥ 80% line | |
| Infrastructure | Not targeted | Integration tests cover it |
| Client | ≥ 60% | Presentation logic only — no business logic exists here |

**Line coverage is a diagnostic, not a goal.** A module at 95% line coverage with an untested
business rule fails the build; one at 70% with every rule tested passes. Optimising line coverage
directly produces tests that assert nothing.

---

**See also:** [Business Rules](../02-domain/05-business-rules.md) · [DevOps & CI/CD](../03-architecture/09-devops-and-cicd.md) · [Definition of Done](04-definition-of-done.md)
