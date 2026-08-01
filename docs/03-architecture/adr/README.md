# Architecture Decision Records

**Status:** Living index · **Owner:** CTO

An ADR records a decision that was **hard to make, expensive to reverse, or surprising to a
newcomer**. Decisions that are obvious do not need one. Decisions made twice because nobody
recorded the first one are the failure this practice exists to prevent.

---

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| [0001](ADR-0001-modular-monolith.md) | Modular monolith over microservices | Accepted | 2026-08-01 |
| [0002](ADR-0002-bitemporal-double-entry-ledger.md) | Bitemporal double-entry ledger as system of record | Accepted | 2026-08-01 |
| [0003](ADR-0003-integer-money.md) | Integer minor-unit money, no floating point | Accepted | 2026-08-01 |
| [0004](ADR-0004-postgres-over-azure-sql.md) | PostgreSQL over Azure SQL | Accepted | 2026-08-01 |
| [0005](ADR-0005-inprocess-event-bus.md) | In-process event bus with transactional outbox | Accepted | 2026-08-01 |
| [0006](ADR-0006-immutable-forecast-artifacts.md) | Immutable, content-addressed forecast artifacts | Accepted | 2026-08-01 |
| [0007](ADR-0007-return-model.md) | Block bootstrap + regime switching over i.i.d. lognormal | Accepted | 2026-08-01 |
| [0008](ADR-0008-llm-narrates-only.md) | Deterministic engine computes; LLM only narrates | Accepted | 2026-08-01 |
| [0009](ADR-0009-attribution-gated-alerting.md) | Attribution-gated alerting; stochastic drivers never alert | Accepted | 2026-08-01 |
| [0010](ADR-0010-manual-first-ingestion.md) | Manual-first ingestion, adapters as ACLs | Accepted | 2026-08-01 |
| [0011](ADR-0011-single-tenant-core.md) | Single-tenant core with tenancy seam | Accepted | 2026-08-01 |
| [0012](ADR-0012-otel-vendor-neutral.md) | OpenTelemetry native, vendor-neutral backend | Accepted | 2026-08-01 |
| [0013](ADR-0013-flutter-client.md) | Flutter single codebase for iOS and web | Accepted | 2026-08-01 |
| [0014](ADR-0014-brazil-only-tax-engine.md) | Brazil-only tax engine behind `ITaxJurisdiction` | Accepted | 2026-08-01 |
| [0015](ADR-0015-cost-ceiling.md) | US$30/month infrastructure cost ceiling as a constraint | Accepted | 2026-08-01 |
| [0016](ADR-0016-no-outcome-gamification.md) | Progression rewards process only, never outcomes | Accepted | 2026-08-01 |
| [0017](ADR-0017-versioned-tax-rulesets.md) | Tax rules as versioned data with effective dates | Accepted | 2026-08-01 |
| [0018](ADR-0018-mixed-persistence.md) | Event sourcing where the log is the truth; state elsewhere | Accepted | 2026-08-01 |
| [0019](ADR-0019-docs-precede-code.md) | Specification precedes implementation; docs in-repo | Accepted | 2026-08-01 |
| [0020](ADR-0020-cryptographic-erasure.md) | Cryptographic erasure to reconcile LGPD with an append-only ledger | Accepted | 2026-08-01 |
| [0021](ADR-0021-dotnet-and-flutter.md) | .NET 10 backend, Dart/Flutter client | Accepted | 2026-08-01 |
| [0022](ADR-0022-advice-posture.md) | Ranked options with disclosed tradeoffs, not prescriptions | Accepted | 2026-08-01 |
| [0023](ADR-0023-bilingual-product-english-spec.md) | Bilingual product surface over an English-only specification | Accepted | 2026-08-01 |

---

## Process

1. Copy [`ADR-0000-template.md`](ADR-0000-template.md), take the next number. **Numbers are never
   reused.**
2. Open a PR with status `Proposed`.
3. Discuss in the PR. The *rejected alternatives* section is the point of the document — an ADR
   that lists no alternatives records a preference, not a decision.
4. Merge as `Accepted`.
5. To reverse: write a **new** ADR that supersedes the old one. Never edit an accepted ADR's
   decision — mark it `Superseded by ADR-nnnn` and leave it intact. The history of wrong turns is
   more valuable than a tidy index.

## Status values

`Proposed` · `Accepted` · `Superseded by ADR-nnnn` · `Deprecated` (no longer relevant, not replaced)
