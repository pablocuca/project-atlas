# Non-Functional Requirements

**Status:** Ratified · **Owner:** CTO / SRE

Every NFR is **measurable** and has a stated verification method. An NFR that cannot be measured is
an aspiration and does not belong here.

---

## NFR-1xx — Correctness *(the highest-priority category)*

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-101 | Monetary arithmetic is exact | Zero floating-point in financial paths | Roslyn analyser, CI |
| NFR-102 | Forecast replay is bit-identical | 100% | Determinism gate, daily sampled check in prod |
| NFR-103 | Twin snapshot canonicalisation is machine-independent | 100% | Cross-platform hash golden test |
| NFR-104 | Attribution residual | ≤ 2% of total delta | Runtime invariant + property test |
| NFR-105 | Ledger entries balance per commodity | 100% | DB constraint + property test |
| NFR-106 | Position quantities reconcile to ledger quantities | 100% | Invariant test (INV-040) |
| NFR-107 | Tax computations carry a complete rule trace | 100% | Type-enforced + golden files |
| NFR-108 | Monte Carlo standard error on `P(FI)` | ≤ 0.5pp | Convergence check, run-rejecting |
| NFR-109 | Historical artifacts remain deserialisable | 100%, all versions, forever | Historical corpus CI test |
| NFR-110 | Every business rule has an automated test | 100% | Rule-coverage CI gate |

> **Correctness outranks everything else in this document.** A wrong number is worse than a slow
> one, worse than an unavailable one, and worse than an ugly one.

## NFR-2xx — Performance

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-201 | Mission Control load (warm) | ≤ 500 ms p95 | Synthetic check |
| NFR-202 | Cold start to first byte | ≤ 8 s p95 | Synthetic check |
| NFR-203 | Quick expense capture, tap to saved | ≤ 10 s including user input | Manual timed test |
| NFR-204 | Ledger write (single entry) | ≤ 100 ms p95 | Load test |
| NFR-205 | Bitemporal balance query | ≤ 200 ms p95 | Load test |
| NFR-206 | Twin snapshot build | ≤ 2 s p95 | Benchmark |
| NFR-207 | Materiality gate decision | ≤ 50 ms | Benchmark |
| NFR-208 | Full forecast (50k paths, 660 months) | ≤ 90 s on 2 vCPU | Benchmark |
| NFR-209 | Attribution (analytic path) | ≤ 2 s | Benchmark |
| NFR-210 | Attribution (sampled Shapley) | ≤ 30 s | Benchmark |
| NFR-211 | Option evaluation set (5 options × 6 scenarios, cached) | ≤ 5 min | Benchmark |
| NFR-212 | CSV import, 1,000 rows | ≤ 30 s | Benchmark |

**Deliberately absent:** any real-time or sub-second data freshness target. NG-06 — the decision
cadence of financial independence is weeks, and pretending otherwise would multiply cost, complexity,
and noise simultaneously.

## NFR-3xx — Availability & recoverability

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-301 | API availability | 99.0% monthly | SLO monitor |
| NFR-302 | Durability of ledger truth | **100% — no acceptable loss** | Backup verification |
| NFR-303 | RPO | ≤ 5 min | PITR configuration + drill |
| NFR-304 | RTO | ≤ 1 h | Quarterly restore drill |
| NFR-305 | Recovery from total Azure loss | ≤ 1 day | Weekly export + annual drill |
| NFR-306 | Backup restore verification | Weekly, 100% pass | CI job |
| NFR-307 | Recording a fact is never blocked by a downstream failure | 100% | Chaos test |

The modest 99% availability target is deliberate and justified in
[Infrastructure §7](../03-architecture/08-infrastructure.md): no decision in this product has a
two-hour deadline, and buying 99.9% would cost ~5× to protect against a harm that does not exist.

## NFR-4xx — Security & privacy

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-401 | Authentication is passwordless (passkey) | 100% | Design review |
| NFR-402 | Secrets present in repo, images, config, or logs | **Zero** | `gitleaks`, CI |
| NFR-403 | Personal identifiers encrypted at field level | 100% | Schema review |
| NFR-404 | Financial values in telemetry or logs | **Zero** | Analyser + log pattern scan |
| NFR-405 | Transport security | TLS 1.3 minimum | Config check |
| NFR-406 | Critical CVE in a production dependency | Zero at deploy time | CI scan, deploy-blocking |
| NFR-407 | LGPD erasure completes | ≤ 24 h, cryptographic | Runbook drill |
| NFR-408 | LLM prompt content | FactSet only, no raw ledger | Prompt inspection test |
| NFR-409 | Service-to-service auth uses managed identity | 100% | Bicep review |
| NFR-410 | SBOM produced per build | 100% | CI artifact |

## NFR-5xx — Cost

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-501 | Total monthly production run-rate | **≤ US$ 30** | Azure Budget + `atlas_monthly_cost_usd` |
| NFR-502 | Cost per forecast run | ≤ US$ 0.05 | Job metering |
| NFR-503 | Storage growth | ≤ 100 MB/year | Metric |
| NFR-504 | Every infra PR states its cost delta | 100% | PR template gate |

## NFR-6xx — Maintainability & evolvability

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-601 | Module boundary violations | Zero | Architecture tests (MR-1..MR-10) |
| NFR-602 | Cyclic module dependencies | Zero | Architecture tests |
| NFR-603 | Domain layer infrastructure dependencies | Zero | Architecture tests |
| NFR-604 | Ambient clock access in domain code | Zero | Roslyn analyser |
| NFR-605 | Progression removable with no financial impact | 100% | `no-frills build` CI job |
| NFR-606 | Narrative removable with no financial impact | 100% | `no-frills build` CI job |
| NFR-607 | Full CI pipeline duration | ≤ 25 min | Pipeline metric |
| NFR-608 | Every ADR-worthy decision has an ADR | 100% | Review |
| NFR-609 | Time for a new maintainer to run the system locally | ≤ 30 min from clone | Documented + periodically re-tested |

## NFR-7xx — Data portability & longevity

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-701 | Full export completes in one command | 100% | CI |
| NFR-702 | Export is self-describing (schema + units travel with data) | 100% | Export schema test |
| NFR-703 | Export uses open formats only | 100% (Parquet, JSON, CSV, zstd) | Review |
| NFR-704 | Export re-imports into a clean database with equality | 100% | CI job |
| NFR-705 | No deletion path exists for ledger, snapshots, or artifacts | 100% | Type + DB permission |
| NFR-706 | Every historical schema version remains readable | 100%, forever | Historical corpus test |

## NFR-8xx — Usability & accessibility

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-801 | Every card states its question | 100% | Build-time validation |
| NFR-802 | Any number traceable to its artifact | ≤ 1 interaction | Design review |
| NFR-803 | Data freshness discoverable per card | ≤ 1 interaction | Design review |
| NFR-804 | Colour contrast | WCAG 2.2 AA | Automated audit |
| NFR-805 | Dynamic Type / text scaling supported | 100% of surfaces | Manual test |
| NFR-806 | VoiceOver navigation of Mission Control | Complete | Manual test |
| NFR-807 | No information conveyed by colour alone | 100% | Design review |
| NFR-808 | Notifications per month at steady state | **≤ 4** | Metric — see below |
| NFR-810 | Message-key coverage across supported locales | 100% | ICU completeness gate |
| NFR-811 | Copy-lint coverage across supported locales | 100% of banned categories | CI, build-failing |
| NFR-812 | Layout integrity at max Dynamic Type, both locales | No truncation or overflow | Manual + snapshot test |
| NFR-813 | `lang` attribute present on every user-facing string | 100% | Accessibility audit |
| NFR-814 | Locale switch takes effect without reinstall or data loss | 100% | Manual test |

**NFR-808 is a genuine and unusual requirement.** Most products set a floor on engagement; Atlas
sets a **ceiling on interruption**. Exceeding four notifications per month at steady state is
treated as a product defect and triggers a Signal Gate review — because it means the gate is
failing and the product is drifting toward the category it was built not to be.

## NFR-9xx — Observability

| ID | Requirement | Target | Verification |
|---|---|---|---|
| NFR-901 | Critical paths traced end to end | 100% | Trace review |
| NFR-902 | Every displayed number links to a snapshot hash in telemetry | 100% | Trace attribute check |
| NFR-903 | Alerts without a runbook | Zero | Alert definition review |
| NFR-904 | Alerts firing more than twice monthly | Zero (fix or delete) | Monthly review |
| NFR-905 | Telemetry vendor SDK references in code | Zero (OTel only) | Analyser |

---

## Quality attribute priority order

When two attributes conflict, resolve in this order. This ordering is itself an architectural
decision, and it differs sharply from a typical consumer app.

```
1. Correctness        A wrong number destroys the product's reason to exist
2. Data durability    Lost truth is unrecoverable
3. Security & privacy This dataset is more sensitive than any single bank's
4. Portability        The data must outlive the platform
5. Maintainability    It must be changeable for twenty years
6. Cost               Low idle cost is a durability requirement
7. Usability          It must be genuinely usable
8. Performance        It must be fast enough — not fast
9. Availability       A two-hour outage costs nothing
```

---

**See also:** [Quality Attributes](04-quality-attributes.md) · [Infrastructure](../03-architecture/08-infrastructure.md) · [Observability Strategy](../03-architecture/07-observability-strategy.md)
