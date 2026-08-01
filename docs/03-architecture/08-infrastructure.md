# Infrastructure

**Status:** Ratified · **Owner:** SRE / CTO · **Last reviewed:** 2026-08-01
**Constraint:** total run-rate **≤ US$ 30/month** ([ADR-0015](adr/ADR-0015-cost-ceiling.md))

---

## 1. Why the cost ceiling is an architectural input, not a preference

A personal system that costs US$150/month has a **finite lifespan measured in motivation**. At
US$20/month it survives indifference, job changes, and years of dormancy. Since Atlas must run for
two decades, **low idle cost is a durability requirement**, and it is treated with the same
seriousness as availability.

The ceiling also produces better architecture: scale-to-zero forces stateless request handling;
avoiding always-on middleware forces the outbox pattern; avoiding per-GB telemetry pricing forces
disciplined cardinality. Every one of those is what a well-built system would do anyway.

---

## 2. Target topology

```
                            Azure Front tier
   ┌──────────────────────────────────────────────────────────────┐
   │  Static Web Apps (Free)          Custom domain + managed TLS │
   │  atlas-web  — Flutter web build                              │
   └──────────────────────────────┬───────────────────────────────┘
                                  │
   ┌──────────────────────────────▼───────────────────────────────┐
   │  Container Apps Environment (Consumption)  — Brazil South     │
   │                                                               │
   │  ┌──────────────────────┐    ┌────────────────────────────┐  │
   │  │ atlas-api            │    │ atlas-sim  (CA Job)        │  │
   │  │ min 0 / max 1        │    │ scheduled + event-triggered│  │
   │  │ 0.5 vCPU / 1 GiB     │    │ 2 vCPU / 4 GiB, ephemeral  │  │
   │  └──────────┬───────────┘    └──────────────┬─────────────┘  │
   └─────────────┼───────────────────────────────┼────────────────┘
                 │                               │
   ┌─────────────▼─────────┐   ┌─────────────────▼────────────────┐
   │ PostgreSQL Flexible   │   │ Storage Account (StorageV2)      │
   │ B1ms · 32 GB · 7d PITR│   │ artifacts (hot) · raw (cool)     │
   └───────────────────────┘   │ backups (cool) · exports (cool)  │
                               └──────────────────────────────────┘
   ┌───────────────────────┐   ┌──────────────────────────────────┐
   │ Key Vault (Standard)  │   │ Observability backend (external) │
   │ secrets + tenant keys │   │ OTLP · free tier                 │
   └───────────────────────┘   └──────────────────────────────────┘

   Registry: GitHub Container Registry (free for the repo) — not ACR
   Identity: Entra ID + passkey; managed identity for service-to-service
```

**Region: Brazil South.** Data residency for a Brazilian tax and financial dataset, and latency to
the primary user. LGPD does not mandate it, but keeping personal financial data in-country removes
a class of argument entirely.

---

## 3. Cost model

| Component | SKU | Basis | Est. US$/mo |
|---|---|---|---|
| Container Apps — `atlas-api` | Consumption, min 0 | ~30 h active/mo, within the monthly free grant | **0.00 – 3.00** |
| Container Apps Job — `atlas-sim` | Consumption | ~30 runs × ~4 min × 2 vCPU | **1.50 – 4.00** |
| PostgreSQL Flexible | B1ms, 32 GB, 7-day PITR | Always-on — **the dominant cost** | **13.00 – 16.00** |
| Storage Account | StorageV2, LRS | < 5 GB, low transaction count | **0.30 – 1.00** |
| Key Vault | Standard | ~10k operations | **0.05** |
| Static Web Apps | Free | Flutter web + custom domain + TLS | **0.00** |
| Container Registry | GHCR | Free for the repository | **0.00** |
| Observability | External free tier via OTLP | Metrics/logs/traces within free limits | **0.00** |
| GitHub Actions | Free tier | CI/CD | **0.00** |
| **Total** | | | **≈ US$ 15 – 24** |

Headroom to the ceiling: ~US$6–15/month, deliberately reserved for growth in simulation compute.

### Cost governance
- **Azure Budget at US$30** with alerts at 50/80/100%, routed to the same notification channel as
  incidents.
- Cost is an **SLI**: `MonthlyRunRate ≤ US$30`. A sustained breach declares a SEV-3
  `CostRegression` incident with a runbook — the same machinery as any other reliability breach.
- Every PR that touches Bicep must state its cost delta in the PR template. "Unknown" is not an
  accepted value.

### Rejected alternatives, and why
| Rejected | Cost | Reason |
|---|---|---|
| Azure SQL Serverless (free tier) | ~$0 | Cheaper, but weaker portability and no first-class bitemporal ergonomics. **AI-3 outranks cost** |
| App Service B1 always-on | ~$13 | No scale-to-zero; strictly worse than Container Apps here |
| Azure Service Bus | ~$10 | Buys nothing at N=1. The `IEventBus` seam already preserves the option |
| Application Insights | ~$5–25, per-GB | Ingestion pricing punishes exactly the telemetry richness Atlas wants. OTel keeps it swappable |
| Azure Container Registry Basic | ~$5 | GHCR is free and equivalent for this use |
| AKS | ~$75+ | Absurd at this scale. Would be a hobby, not a solution |

---

## 4. Infrastructure as Code

**Bicep**, not Terraform — Azure-only scope, no state file to host or corrupt, first-party resource
coverage on day one. The trade (multi-cloud portability) is irrelevant given that the *data* exit
path, not the *infrastructure* one, is what AI-3 actually protects.

```
infra/
  main.bicep                  subscription-scope entry point
  modules/
    network.bicep             Container Apps environment
    data.bicep                Postgres, storage
    compute.bicep             api app, sim job
    security.bicep            Key Vault, managed identities, RBAC
    observability.bicep       diagnostic settings, OTLP secret wiring
    budget.bicep              cost budget + alerts
  environments/
    dev.bicepparam            minimal, may be torn down nightly
    prod.bicepparam
```

Rules:
- **No resource is ever created in the portal.** Drift is detected by a weekly
  `az deployment what-if` in CI; any drift opens an issue automatically.
- Every resource is tagged `project=atlas`, `env`, `owner`, `costCenter`.
- Deletion locks on Postgres, the storage account, and Key Vault.

---

## 5. Environments

| Environment | Purpose | Data | Lifetime |
|---|---|---|---|
| `local` | Development | Docker Compose: Postgres + Azurite. Synthetic fixtures | Ephemeral |
| `dev` | Integration verification | Synthetic only — **never production data** | Torn down nightly |
| `prod` | The real system | Real | Permanent |

Deliberately **no staging environment**: at N=1 it would double the dominant cost (Postgres) to
guard against a risk already covered by contract tests, the deterministic-replay gate, and
one-command rollback. Recorded as an accepted risk (`RISK-014`) rather than an oversight.

---

## 6. Runtime configuration

| Concern | Mechanism |
|---|---|
| Secrets | Key Vault via managed identity. **Zero secrets in env vars, config files, or the repo** |
| Feature flags | Postgres-backed, module-scoped, read at request start |
| Model versions | Explicit config, never "latest". Changing it is a deploy and emits `ModelVersionPublished` |
| Tax ruleset version | Data, versioned with effective dates. Ships as content, not code |
| Time zone | System is **UTC everywhere**; `America/Sao_Paulo` applied at presentation and for tax-day boundaries only |

---

## 7. Availability posture

**Target: 99% monthly for the API** — deliberately modest, and deliberately explicit.

Atlas is not a trading system. A two-hour outage costs the user nothing, because no decision in
this product has a two-hour deadline. Pursuing 99.9% would require multi-instance always-on
compute and a zone-redundant database — roughly 5× the cost — to protect against a harm that does
not exist. Stating the target *down* is as much an engineering act as stating it up.

What genuinely matters instead:

| Property | Target | Rationale |
|---|---|---|
| **Durability of truth** | 100%, no acceptable loss | A lost ledger entry is unrecoverable |
| **Recoverability** | RTO ≤ 1 h, RPO ≤ 5 min | Postgres PITR + artifact immutability |
| **Correctness** | Zero tolerance | A wrong number is far worse than an unavailable one |
| **Cold start** | ≤ 8 s p95 | Acceptable for a system used a few times a week |

Cold start is an accepted, measured trade-off — the direct price of scale-to-zero, and cheap at
this usage pattern.

---

## 8. Disaster recovery

| Scenario | Response | Target |
|---|---|---|
| Container Apps revision failure | Automatic rollback to last healthy revision | < 5 min |
| Database corruption | PITR restore to pre-incident timestamp | < 1 h |
| Region outage (Brazil South) | Redeploy from Bicep to Brazil Southeast; restore from geo-backup | < 4 h |
| **Azure account loss** | Restore from the weekly local export onto any container host | < 1 day |
| Subscription cancellation | Same as above — the export is the true DR asset | < 1 day |

The last two are the reason the export path is CI-verified weekly (BR-A00). A recovery path that
has never been exercised is a hypothesis, not a plan.

---

**See also:** [DevOps & CI/CD](09-devops-and-cicd.md) · [Observability Strategy](07-observability-strategy.md) · [ADR-0015](adr/ADR-0015-cost-ceiling.md)
