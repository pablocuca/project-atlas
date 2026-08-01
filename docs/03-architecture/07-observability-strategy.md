# Observability Strategy

**Status:** Ratified · **Owner:** SRE · **Last reviewed:** 2026-08-01

Atlas has **two observability planes**, and conflating them is a design error:

| Plane | Subject | Audience | Home |
|---|---|---|---|
| **System observability** | Is the software healthy? | The operator as engineer | This document |
| **Financial reliability** | Is the *financial life* healthy? | The operator as person | [Financial Reliability Model](../04-engines/08-financial-reliability-model.md) |

They share vocabulary (SLI, SLO, error budget, incident) deliberately — the same mental model
applied to two domains — but they never share dashboards, alert channels, or severity scales. A
failing database must not appear next to a failing savings rate.

---

## 1. Principles

1. **OpenTelemetry native, vendor-neutral.** Instrument once with OTel; the backend is a config
   change ([ADR-0012](adr/ADR-0012-otel-vendor-neutral.md)). This is AI-3 applied to telemetry.
2. **Cardinality is a budget.** Per-GB backends make rich telemetry expensive; the discipline that
   imposes is healthy. Every label is justified; unbounded labels (ids, amounts) are banned.
3. **Traces are the primary signal**, metrics the aggregate, logs the last resort. Logs are the
   most expensive and least structured of the three.
4. **Every business-critical path is traced end to end** — import → post → snapshot → forecast →
   attribute → gate is one trace.
5. **Telemetry never contains financial data** ([Security §7](06-security-strategy.md)).

---

## 2. Instrumentation

### Traces

Root spans, one per meaningful operation:

```
atlas.ingest.batch          → parse → normalise → dedupe → propose → post
atlas.ledger.post_entry     → validate → append → outbox
atlas.twin.snapshot         → gather(×6 modules) → canonicalise → hash → store
atlas.forecast.run          → load_snapshot → simulate → aggregate → store_artifact
atlas.attribution.compute   → load_pair → decompose → classify → gate
atlas.advisory.enumerate    → policy_space → evaluate(×N counterfactuals) → rank
atlas.calibration.score     → observe → score → update_curve
atlas.narrative.render      → build_factset → generate → verify_traceability
```

Mandatory span attributes (all low-cardinality, all non-financial):

| Attribute | Example |
|---|---|
| `atlas.module` | `forecast` |
| `atlas.loop` | `foresight` |
| `atlas.tenant_hash` | first 8 hex of hashed tenant id |
| `atlas.model_version` | `2.1.0` |
| `atlas.snapshot_hash` | first 12 hex — links a trace to an artifact |
| `atlas.outcome` | `ok` \| `degraded` \| `rejected` |
| `atlas.degradation_reason` | `stale_marks` \| `low_coverage` \| `unreconciled` |

`atlas.snapshot_hash` is the highest-value attribute in the system: it is what makes a *user-visible
number* traceable back to *the exact computation that produced it*, years later.

### Metrics (RED + USE + domain)

| Metric | Type | Labels |
|---|---|---|
| `atlas_requests_total` | counter | module, endpoint, status |
| `atlas_request_duration_seconds` | histogram | module, endpoint |
| `atlas_errors_total` | counter | module, error_code |
| `atlas_forecast_duration_seconds` | histogram | model_version, path_count_bucket |
| `atlas_forecast_paths_total` | counter | model_version |
| `atlas_forecast_determinism_failures_total` | counter | model_version |
| `atlas_snapshot_build_duration_seconds` | histogram | — |
| `atlas_materiality_gate_decisions_total` | counter | decision (`recompute`/`defer`) |
| `atlas_signal_gate_decisions_total` | counter | decision (`emit`/`suppress`), driver_class |
| `atlas_outbox_lag_seconds` | gauge | — |
| `atlas_outbox_dead_letters_total` | counter | event_type |
| `atlas_ingestion_rows_total` | counter | source_kind, outcome |
| `atlas_reconciliation_drift_detected_total` | counter | source_kind |
| `atlas_mark_staleness_hours` | gauge | commodity_kind |
| `atlas_llm_narration_rejected_total` | counter | reason |
| `atlas_monthly_cost_usd` | gauge | resource_type |

**Two of these are load-bearing product metrics, not just ops metrics:**
- `atlas_signal_gate_decisions_total` — the suppress:emit ratio *is* the measurement of Law 9.
  A ratio trending toward 1:1 means the gate is failing and the product is becoming noisy.
- `atlas_forecast_determinism_failures_total` — any non-zero value means AI-2 is broken and every
  historical comparison is suspect. It pages.

### Logs

Structured JSON, allow-listed fields only. Levels:

| Level | Use | Retention |
|---|---|---|
| `Error` | Unhandled or domain-invariant violation | 90 days |
| `Warning` | Degradation, rejected attribution, dead-letter | 30 days |
| `Information` | Lifecycle, deploys, migrations, gate decisions | 14 days |
| `Debug` | Off in production; enabled per-request via header + re-auth | — |

---

## 3. System SLIs and SLOs

| SLI | Definition | SLO | Budget |
|---|---|---|---|
| API availability | Non-5xx ÷ total, excluding cold start | 99.0% / 30d | 7.2 h |
| API latency (warm) | p95 of read endpoints | ≤ 500 ms | — |
| Cold start | p95 time to first byte after scale-from-zero | ≤ 8 s | — |
| Forecast freshness | Age of the newest artifact vs newest material state change | ≤ 24 h | — |
| Forecast success | Successful runs ÷ attempted | ≥ 99% | — |
| **Determinism** | Replay-equality check on a sampled artifact per day | **100%** | **zero** |
| Outbox lag | p99 event dispatch delay | ≤ 60 s | — |
| Dead letters | Events in dead-letter queue | 0 | zero |
| Data freshness | Hours since last successful reconciliation per source | ≤ 72 h | — |
| Backup verification | Weekly restore-and-compare passes | 100% | zero |
| Cost | Monthly run-rate | ≤ US$ 30 | — |

Zero-budget SLOs (determinism, dead letters, backup verification) are correctness properties, not
performance targets. Any breach is an incident, not a budget consumption.

---

## 4. Alerting

Applying Law 9 to the system plane as well: **alerts must be actionable, rare, and routed by
severity.**

| Severity | Criterion | Channel | Response |
|---|---|---|---|
| **SEV-1** | Data loss risk, determinism failure, security event | Push, immediate | Now |
| **SEV-2** | Forecast pipeline down > 24 h, dead letters, backup failure | Push, business hours | Same day |
| **SEV-3** | Sustained availability breach, cost overrun, reconciliation drift | Daily digest | This week |
| **SEV-4** | Latency drift, dependency CVE, mark staleness | Weekly digest | Backlog |

Burn-rate alerting (multi-window: 1 h fast + 6 h slow) for availability, so a short blip does not
page and a slow bleed does not go unnoticed.

**Anti-alert rules:**
- No alert without a runbook. Creating an alert requires writing `RB-*` first.
- No alert that fires more than twice a month survives review — it is either fixed or deleted.
- **No financial condition ever reaches the system alert channel**, and no system condition ever
  reaches the user's financial channel.

---

## 5. Dashboards

| Dashboard | Answers | Refresh |
|---|---|---|
| **Ops Overview** | Is the software up and healthy? | 1 min |
| **Pipeline** | Is truth flowing → ledger → twin → forecast? | 5 min |
| **Data Quality** | Coverage, freshness, reconciliation, categorisation | 1 h |
| **Cost** | Run-rate vs ceiling, by resource | 1 day |
| **Model Health** | Determinism, forecast duration, gate ratios, calibration drift | 1 h |

Dashboards obey the same law as product surfaces: **every panel states the question it answers**
(Law 1). A panel with no question is deleted.

---

## 6. Backend choice and exit

Ship with an **OTLP-compatible free-tier backend**; the decision is deliberately reversible.

| Candidate | Free tier | Trade |
|---|---|---|
| Grafana Cloud (chosen) | Generous free tier for metrics/logs/traces | Best fit for the cost ceiling; strong OTel support |
| Azure Monitor / App Insights | Per-GB ingestion | Deeper Azure integration, but pricing penalises rich telemetry |
| Self-hosted LGTM stack | Compute cost | Full control; violates the cost ceiling on a hosted VM |

Because instrumentation is pure OTel, switching is an OTLP endpoint change plus dashboard
re-authoring. **No Atlas code changes.** That is the entire value of the vendor-neutral bet, and
the reason it is worth a small amount of extra work today.

---

## 7. What is deliberately not instrumented

| Not instrumented | Why |
|---|---|
| Per-transaction detail | Financial data must not leave the boundary |
| User interaction analytics | Engagement is an anti-metric (NG-11). Atlas does not want this data |
| Real-user monitoring with session replay | Would capture financial data on screen |
| Per-commodity metrics | Unbounded cardinality, no operational value |

The absence of product analytics is a deliberate position: Atlas cannot be optimised for engagement
**because it does not measure engagement**. Removing the temptation is more reliable than resisting it.

---

**See also:** [Financial Reliability Model](../04-engines/08-financial-reliability-model.md) · [Infrastructure](08-infrastructure.md) · [Security Strategy](06-security-strategy.md)
