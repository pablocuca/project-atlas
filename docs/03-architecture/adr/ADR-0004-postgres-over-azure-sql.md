# ADR-0004 — PostgreSQL over Azure SQL

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, SRE

## Context

The user is an Azure specialist, and Azure SQL Database currently offers a free serverless tier
that would make the database effectively **free** — against a PostgreSQL Flexible Server B1ms at
~US$14/month, which is the single largest line item in the entire budget.

So the cheaper, more Azure-native option loses here, and the reason needs recording.

## Decision

We will use **PostgreSQL Flexible Server (B1ms)** as the primary datastore, accepting ~US$14/month
against a near-free alternative.

## Rationale

Architectural invariant **AI-3 — data portability outranks platform integration** — is decisive.

- **Exit path.** PostgreSQL runs on any cloud, any VM, a Mac Mini, or a Raspberry Pi. Azure SQL runs
  on Azure. For a system meant to outlive its platform, that asymmetry is the whole argument.
- **Bitemporal ergonomics.** Range types, exclusion constraints, `GiST`/`BRIN` indexing, and rich
  interval operators make the bitemporal ledger natural rather than emulated.
- **Schema-per-module with role isolation** ([Modular Monolith §4](../03-modular-monolith.md)) is
  cleaner and better supported in PostgreSQL.
- **Open ecosystem** for the analytical layer: `pg_stat_statements`, TimescaleDB if time series
  ever justify it, `postgres_fdw` for the export path.
- US$14/month is comfortably inside the ceiling. The ceiling exists to prevent *drift*, not to
  force the cheapest choice on every line.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Azure SQL serverless (free tier) | Free; auto-pause; deep Azure integration | Vendor-locked data; weaker interval/range support; T-SQL lock-in | AI-3 outranks cost |
| SQLite | Free; embedded; trivially portable | No concurrent writer story; weak for schema isolation and roles; awkward for a multi-container topology | Portability yes, but too limited for the topology |
| Cosmos DB | Global scale; serverless | Wrong model for relational bitemporal truth; expensive at any real RU; strong lock-in | Solves problems Atlas doesn't have |
| Postgres in a container with a volume | Cheapest | Backup, PITR, patching all become our toil | Managed backups + PITR are worth US$14 |

## Consequences

### Positive
- The single largest lock-in risk is removed at a known, small, fixed price.
- Bitemporal modelling is idiomatic rather than emulated.
- Local development matches production exactly (same engine in Docker Compose).

### Negative — accepted costs
- ~US$14/month of always-on cost, in an otherwise scale-to-zero architecture. It is the reason the
  budget is ~US$18 rather than ~US$4.
- Burstable B-series can throttle under sustained load. Acceptable at this volume; monitored.

## Reversal cost

**Moderate.** Schema and queries would need porting, but no domain code changes (repositories are
behind ports). The ledger export format is engine-neutral.

## Compliance

Bicep provisions Postgres only. CI runs against the same major version. The export/restore
verification job proves engine-independence of the data.

## References
[Infrastructure §3](../08-infrastructure.md) · [Data Strategy](../04-data-strategy.md) · [ADR-0015](ADR-0015-cost-ceiling.md)
