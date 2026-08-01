# DevOps & CI/CD

**Status:** Ratified · **Owner:** SRE · **Last reviewed:** 2026-08-01

---

## 1. Principles

1. **`main` is always deployable.** Trunk-based development, short-lived branches, no long-running
   integration branches.
2. **The pipeline is the only path to production.** No manual deploys, no portal changes, no
   `kubectl`-equivalent escape hatches.
3. **Gates are objective.** A human may not wave a failing gate through; they may only add an ADR
   changing the gate.
4. **Rollback beats fix-forward** for anything user-facing. One command, under five minutes.
5. **Migrations are expand/contract.** No destructive down-migration ever runs against production
   truth data.

---

## 2. Branching and commits

```
main ──────●────────●────────●────────●──────▶  always deployable, protected
            \      /          \      /
             feat/x            fix/y            ≤ 3 days, squash-merged
```

Conventional Commits, with Atlas-specific scopes:

```
feat(forecast): add regime-switching return model
fix(ledger): correct bitemporal query at decision-time boundary
docs(adr): ADR-0023 accept Grafana Cloud as telemetry backend
refactor(tax): extract come-cotas into a versioned ruleset
test(attribution): property test for BR-201 exact-sum invariant
```

Commit body must cite affected `BR-`, `INV-`, or `FR-` identifiers where applicable. This is what
makes the traceability chain (requirement → rule → test → commit) actually navigable in 2034.

---

## 3. Pipeline

```
PR opened
  ├─ 1. Build & analysers          (~2 min)
  ├─ 2. Architecture tests         (~1 min)   MR-1..MR-10
  ├─ 3. Unit + property tests      (~4 min)
  ├─ 4. Golden-file tests          (~2 min)   tax, adapters, canonicalisation
  ├─ 5. Determinism gate           (~3 min)   AI-2
  ├─ 6. Historical corpus test     (~1 min)   BR-A03
  ├─ 7. Contract tests             (~1 min)   every Published Language
  ├─ 8. no-frills build            (~2 min)   BR-804
  ├─ 9. Security scans             (~2 min)   secrets, deps, SBOM, image
  ├─ 10. Rule-coverage gate        (~1 min)   every BR- has a test
  ├─ 11. Bicep what-if + cost delta(~1 min)
  └─ 12. Flutter analyze + test    (~3 min)
                                    ────────
                                    ~23 min

merge to main
  ├─ Build & push images (GHCR), SBOM + provenance attestation
  ├─ Deploy dev → smoke tests → integration suite
  ├─ Deploy prod (new revision, 100% traffic — single user, no canary value)
  ├─ Post-deploy verification: health, migration state, determinism sample
  └─ Export verification (weekly): full export → clean restore → compare
```

### Gate detail — the five that are unusual

| Gate | What it does | Why it exists |
|---|---|---|
| **Determinism** | Re-runs a fixed corpus of `(snapshot, model, seed)` and asserts bit-identical artifacts | AI-2. Without it, every historical comparison silently rots |
| **Historical corpus** | Deserialises one snapshot + artifact of every schema version ever shipped | BR-A03. Makes "readable in 2046" a build fact |
| **no-frills build** | Compiles and runs the full financial test suite with `Progression` and `Narrative` removed | BR-804. Proves gamification and AI are genuinely peripheral |
| **Rule coverage** | Parses `05-business-rules.md`, asserts every `BR-` has a test annotated with it | A rule with no test is a wish |
| **Cost delta** | Fails if a Bicep change has no stated cost impact in the PR body | Cost is an architectural constraint, not an afterthought |

---

## 4. Environments and promotion

| Environment | Trigger | Data | Gate to enter |
|---|---|---|---|
| `local` | Developer | Synthetic fixtures | — |
| `dev` | Merge to `main` | Synthetic only | All PR gates green |
| `prod` | Automatic after `dev` smoke tests | Real | Smoke + integration green |

No staging ([Infrastructure §5](08-infrastructure.md)) — an accepted, recorded risk (`RISK-014`),
mitigated by contract tests, the determinism gate, and fast rollback.

### Database migrations

**Expand/contract, always:**

```
Release N   : add new column/table (nullable, defaulted). Deploy code writing both.
Release N+1 : backfill. Verify.
Release N+2 : switch reads. Verify.
Release N+3 : contract — drop old. ONLY for projections, NEVER for truth tables.
```

Truth tables (`ledger.*`) are **additive forever** — no column is dropped, renamed, or repurposed
for the life of the project. Migrations run at startup under a Postgres advisory lock, per module,
in dependency order. A failed migration aborts startup; the previous revision keeps serving.

---

## 5. Release and versioning

| Artifact | Scheme | Notes |
|---|---|---|
| Application | CalVer `YYYY.MM.PATCH` | Continuous delivery; the date is the useful fact |
| `ModelVersion` | SemVer | **Major** = incomparable forecasts. Triggers `ModelVersionPublished` and a labelled discontinuity in every trend |
| `TaxRulesetVersion` | SemVer + effective date range | Ships as data; retroactive corrections are a new version, never an edit |
| Contracts (PL) | SemVer | Breaking change requires an ADR |
| Snapshot / artifact schema | Integer, monotonic | Readers retained forever |

**Model version changes are the most dangerous release type in the system.** They require:
1. An ADR describing the change and its expected effect on outputs.
2. A back-test on the historical snapshot corpus, with the output delta quantified and reviewed.
3. A `ModelChange` annotation in every affected trend, so the user never mistakes a model upgrade
   for a change in their life (BR-204).

---

## 6. Rollback

| Failure | Action | Time |
|---|---|---|
| Bad app revision | `az containerapp revision set-mode` → previous revision | < 5 min |
| Bad migration | Previous revision keeps running; forward-fix under a feature flag | < 30 min |
| Bad model version | Config revert to previous `ModelVersion`; artifacts from the bad version are **retained and labelled**, never deleted | < 5 min |
| Corrupt projection | Drop and rebuild from the ledger | Minutes |
| Corrupt truth | PITR restore | < 1 h |

Note the asymmetry: projections are rebuildable and models are revertible, but **truth is only ever
restorable**. That asymmetry is why the ledger gets the strictest gates.

---

## 7. Local development

```bash
docker compose up -d          # postgres + azurite
dotnet run --project src/Atlas.Host
cd app && flutter run
```

`atlas-seed` generates a synthetic 10-year financial life — deterministic from a seed, realistic in
distribution, containing **no real personal data**. Every developer, every CI run, and every demo
uses it. Real data never leaves `prod`, and there is no tooling to copy it out.

---

## 8. Toil and automation policy

Following Google SRE: **toil is capped at 25% of engineering time**, and at N=1 that is measured
honestly or not at all.

| Recurring task | Automation |
|---|---|
| Dependency updates | Renovate, weekly, auto-merge on green for patch/minor |
| Certificate renewal | Managed by Static Web Apps / Container Apps |
| Backup verification | Weekly CI job, restore-and-compare |
| Drift detection | Weekly `az deployment what-if`, opens an issue on drift |
| Cost review | Monthly automated report against the ceiling |
| Restore drill | Quarterly, calendar-triggered, written up |
| Calibration review | Quarterly, calendar-triggered |
| Tax ruleset review | Annual + on legislative change, calendar-triggered |

Anything done manually **three times** becomes an automation task in the backlog. That rule is
written down precisely because at N=1 there is nobody else to notice the toil accumulating.

---

**See also:** [Infrastructure](08-infrastructure.md) · [Testing Strategy](../05-engineering/03-testing-strategy.md) · [Definition of Done](../05-engineering/04-definition-of-done.md)
