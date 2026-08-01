# Architecture Vision

**Status:** Ratified · **Owner:** CTO / Principal Architect · **Last reviewed:** 2026-08-01

---

## 1. The spine: Four Loops

Atlas is not a request/response application with analytics bolted on. It is **four closed loops**
running at different frequencies over one immutable event log. Every component belongs to exactly
one loop, and the loops are the primary decomposition of the system.

```
                    ┌──────────────────────────────────────────────┐
                    │            ① TRUTH LOOP  (minutes)           │
   sources ────────▶│  ingest → ACL → propose → post → reconcile   │
                    │              ↓                               │
                    │         LEDGER (append-only, bitemporal)     │
                    └──────────────┬───────────────────────────────┘
                                   │  materiality gate
                    ┌──────────────▼───────────────────────────────┐
                    │          ② FORESIGHT LOOP  (hours)           │
                    │  twin state → SNAPSHOT → simulate → ARTIFACT │
                    └──────────────┬───────────────────────────────┘
                                   │
                    ┌──────────────▼───────────────────────────────┐
                    │          ③ DECISION LOOP  (days)             │
                    │  Δ → attribute → SIGNAL GATE → SLO/incident  │
                    │       → policy space → rank options          │
                    └──────────────┬───────────────────────────────┘
                                   │
                    ┌──────────────▼───────────────────────────────┐
                    │       ④ CALIBRATION LOOP  (months–years)     │
                    │  observe outcome → score → reliability →     │
                    │  gate advice ─────────────────────┐          │
                    └───────────────────────────────────┼──────────┘
                                                        │
                            feeds back into ③ ◀─────────┘
```

**Why this decomposition and not layers.** Layered architectures decompose by *technical role* and
therefore scatter each business capability across every layer. Loop decomposition matches the
system's actual behaviour: different data lifetimes, different failure modes, different SLAs,
different cost profiles. The Truth Loop must be correct and cheap; the Foresight Loop must be
reproducible and is compute-heavy; the Decision Loop must be *quiet*; the Calibration Loop must be
patient and never touched.

| Loop | Frequency | Optimised for | Failure mode to design against |
|---|---|---|---|
| ① Truth | Event-driven, minutes | Correctness, idempotency | Silent duplication or lost correction |
| ② Foresight | Materiality-gated + daily | Reproducibility, cost | Non-determinism destroying comparability |
| ③ Decision | Daily / on change | **Silence** | Alert fatigue; noise presented as signal |
| ④ Calibration | Continuous, matures over years | Patience, integrity | Being quietly disabled or retuned |

---

## 2. Architectural style

**A modular monolith with strict compile-time module boundaries, event-driven internally, deployed
as a small number of scale-to-zero containers.**

| Decision | Choice | ADR |
|---|---|---|
| Decomposition | Modular monolith; module = bounded context | [ADR-0001](adr/ADR-0001-modular-monolith.md) |
| Communication | In-process event bus behind `IEventBus`, transactional outbox | [ADR-0005](adr/ADR-0005-inprocess-event-bus.md) |
| Persistence | PostgreSQL, bitemporal schema, no ORM in the ledger write path | [ADR-0004](adr/ADR-0004-postgres-over-azure-sql.md) |
| Domain purity | Clean Architecture: domain has zero infrastructure dependencies | [ADR-0001](adr/ADR-0001-modular-monolith.md) |
| Compute | Azure Container Apps, consumption, scale-to-zero; heavy sims as Jobs | [ADR-0015](adr/ADR-0015-cost-ceiling.md) |
| Client | Flutter, single codebase for iOS + web | [ADR-0013](adr/ADR-0013-flutter-client.md) |
| Telemetry | OpenTelemetry native, vendor-neutral backend | [ADR-0012](adr/ADR-0012-otel-vendor-neutral.md) |

### Why not microservices, when the ambition is decades?

Because the failure mode of a decade-long single-maintainer project is **not** scaling — it is
**coupling that was never made visible**. Microservices make coupling *expensive* but not
*visible*; they trade a refactor problem for a distributed-systems problem, and pay operational
cost from day one for scale that will never arrive at N=1.

The modular monolith gets the same benefit — enforced boundaries — at compile time, for free, with
in-process refactoring still available. The **seams are what matter**, not the process boundaries.
Every module already communicates only via published contracts and events, so any module can be
extracted later by replacing the in-process bus with Service Bus behind the *same interface*.

**Extraction trigger, written down now so it isn't argued about later:** a module is extracted only
when it has a *materially different scaling, cost, or availability profile* than the host — the
Simulation runner is the only current candidate, and it is already isolated as a Job.

---

## 3. The three architectural invariants

These outrank every other technical consideration in the system.

### AI-1 — The ledger is the only durable truth
Everything else is a projection, rebuildable from the ledger and market data. Any state that
cannot be reconstructed is an architectural defect. Consequence: projections may be dropped and
rebuilt at will, which makes schema migration of read models a non-event.

### AI-2 — Reproducibility over convenience
Any output the user has ever seen must be reproducible years later. Consequence: snapshots and
artifacts are content-addressed and immutable; models are versioned; time is always an explicit
parameter, never `DateTime.Now` inside a domain computation. Enforced by an analyser that bans
ambient clock access in domain assemblies.

### AI-3 — Data portability outranks platform integration
Where an Azure-native service would create a data format Atlas cannot export, the Azure service
loses. Consequence: PostgreSQL over Azure SQL; Parquet/JSON-LD for artifacts; blob storage used as
a dumb byte store.

---

## 4. Runtime topology

```
┌───────────────────────────────────────────────────────────────────┐
│  CLIENT                                                            │
│  Flutter app — iOS (primary), Web (secondary), offline-capable     │
└─────────────────────────────┬─────────────────────────────────────┘
                              │ HTTPS / JSON, OIDC
┌─────────────────────────────▼─────────────────────────────────────┐
│  atlas-api            Azure Container Apps · scale-to-zero         │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  Composition root                                            │  │
│  │  ┌────────┬────────┬────────┬────────┬────────┬───────────┐ │  │
│  │  │Ledger  │Position│Cashflow│Liab.   │Human   │Goals      │ │  │
│  │  ├────────┼────────┼────────┼────────┼────────┼───────────┤ │  │
│  │  │Twin    │Forecast│Attrib. │Advisory│Calib.  │Reliability│ │  │
│  │  ├────────┴────────┴────────┴────────┴────────┴───────────┤ │  │
│  │  │Taxation (pure)  │Progression│Narrative│Ingestion│Market │ │  │
│  │  └──────────────────────────────────────────────────────────┘ │
│  │  IEventBus → in-process dispatcher + transactional outbox    │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────┬─────────────────────────┬──────────────────────┬───────────┘
       │                         │                      │
┌──────▼──────┐   ┌──────────────▼─────────┐   ┌────────▼──────────┐
│ PostgreSQL  │   │ atlas-sim (CA Job)     │   │ Blob Storage      │
│ Flexible    │   │ Monte Carlo runner     │   │ artifacts,        │
│ B1ms        │   │ scale-to-zero, nightly │   │ snapshots, export │
└─────────────┘   └────────────────────────┘   └───────────────────┘
       │                         │                      │
       └────────── OpenTelemetry (OTLP) ────────────────┘
                              │
                   ┌──────────▼──────────┐   ┌──────────────┐
                   │ Observability       │   │ Key Vault    │
                   │ backend (swappable) │   │ secrets      │
                   └─────────────────────┘   └──────────────┘
```

**Three deployable units, deliberately:**
`atlas-api` (the monolith), `atlas-sim` (compute-isolated simulation Job), `atlas-web` (static
Flutter web on Static Web Apps free tier). Simulation is separated not for modularity but because
its **cost and scaling profile genuinely differ** — it is bursty, CPU-bound, and must not hold the
API's memory hostage.

---

## 5. Write path and read path

**Command (write) path** — synchronous, transactional, strongly consistent:
```
Command → Validation → Aggregate → Domain events → Same transaction:
  { state rows, event rows, outbox rows } → commit → outbox dispatch
```
Transactional outbox rather than direct publish: it makes "state changed but event lost" impossible
without a distributed transaction ([ADR-0005](adr/ADR-0005-inprocess-event-bus.md)).

**Query (read) path** — projections, eventually consistent, always rebuildable:
```
Event → Projector → Read model (Postgres table or materialised view) → API → Client
```
CQRS at the *model* level, not the infrastructure level: same database, different tables. Read
models are disposable by AI-1.

**Why not full event sourcing everywhere?** The ledger *is* an event store by nature, and event
sourcing it is free. But event-sourcing a `SpendingModel` — a fitted statistical object — adds
ceremony with no benefit. Event sourcing is applied where the event log *is* the domain truth
(Ledger, Policy, Goals, Incidents) and conventional state persistence elsewhere. Mixed persistence
styles are a deliberate, documented choice, not an inconsistency.

---

## 6. Failure philosophy

| Principle | Applied |
|---|---|
| **Degrade, never lie** | Stale marks ⇒ forecast runs and is flagged `Degraded`, never silently substituted |
| **Fail closed on advice** | Any doubt about reliability ⇒ suppress the recommendation and say why |
| **Fail open on recording** | Never block the user from recording a fact because a downstream engine is unavailable |
| **Never lose an input** | Ingestion writes raw payloads to blob before parsing; a parse failure is recoverable forever |
| **Idempotent everywhere** | Every command and every handler is safe to retry. At-least-once delivery is assumed |
| **No silent catch** | An exception either becomes a domain error with a code, or crashes loudly |

---

## 7. Evolution path (deliberately pre-decided)

| Trigger | Response | Cost |
|---|---|---|
| Simulation exceeds Job time budget | Fan out `atlas-sim` into parallel Job replicas | Config only |
| Second user / open-sourcing | Activate tenancy seam; `TenantId` already present | Migration, not rewrite |
| Postgres becomes a bottleneck | Read replicas, then extract read models | Standard scaling |
| In-process bus insufficient | Swap `IEventBus` to Service Bus adapter | One adapter, one config |
| Second jurisdiction | New `ITaxJurisdiction` implementation | Additive |
| Azure exit | Containers + Postgres run anywhere; artifacts are open format | Days, by design (AI-3) |

Each of these is an *option deliberately kept open at near-zero carrying cost*. That is the whole
point of the seam-first approach: pay for optionality in design, not in infrastructure.

---

**See also:** [Modular Monolith](03-modular-monolith.md) · [Data Strategy](04-data-strategy.md) · [Infrastructure](08-infrastructure.md) · [ADR Index](adr/README.md)
