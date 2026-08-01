# Modular Monolith

**Status:** Ratified · **Owner:** Principal Architect · **Last reviewed:** 2026-08-01

A modular monolith that is not *enforced* is just a monolith with good intentions. This document
specifies the boundaries and — more importantly — the machinery that makes violating them fail the
build.

---

## 1. The module rule

> **One bounded context = one module = one .NET project pair (`Domain` + `Infrastructure`) + one
> public contracts project.**

```
Atlas.Modules.Ledger.Domain          — entities, value objects, invariants. No dependencies.
Atlas.Modules.Ledger.Application     — commands, handlers, ports (interfaces)
Atlas.Modules.Ledger.Infrastructure  — Postgres, adapters. Implements ports.
Atlas.Modules.Ledger.Contracts       — the ONLY project other modules may reference
```

**The contracts project contains only:** published domain events, query DTOs, and port interfaces
offered as Open Host Services. It has no behaviour and depends only on `Atlas.Kernel`.

---

## 2. Reference rules (build-enforced)

| # | Rule |
|---|---|
| MR-1 | `X.Domain` may reference **only** `Atlas.Kernel`. Nothing else. Ever |
| MR-2 | `X.Application` may reference `X.Domain`, `Atlas.Kernel`, and **other modules' `.Contracts` only** |
| MR-3 | `X.Infrastructure` may reference `X.Application`, `X.Domain`, and infrastructure packages |
| MR-4 | **No module may reference another module's `.Domain`, `.Application`, or `.Infrastructure`** |
| MR-5 | Only `Atlas.Host` (composition root) may reference every `.Infrastructure` |
| MR-6 | `Atlas.Kernel` references nothing |
| MR-7 | `Taxation.Domain` may not reference `Atlas.Kernel.Time` or any I/O abstraction — it is pure |
| MR-8 | `Progression.*` may not reference `Position`, `Valuation`, `Forecast`, or `MarketData` contracts |
| MR-9 | `Narrative.*` may reference **only** `Advisory.Contracts`, `Attribution.Contracts`, `Reliability.Contracts` |
| MR-10 | Cyclic module dependencies are forbidden, including through contracts |

### Enforcement

Three independent mechanisms, because one is always circumvented eventually:

1. **Architecture tests** (`NetArchTest` / `ArchUnitNET`) in `Atlas.ArchitectureTests`, run in CI.
   Each rule above is one named test citing its `MR-` id.
2. **`Directory.Build.props` guards** — a build target that inspects `ProjectReference` items and
   fails on any reference matching a forbidden pattern. Catches violations before compilation.
3. **Roslyn analysers** for the subtler rules: banned `DateTime.Now`/`DateTimeOffset.Now` in
   `*.Domain`, banned `double`/`float` in financial types, banned direct `DbContext` access outside
   `*.Infrastructure`.

> **Why three.** Architecture tests can be deleted. Build props can be edited. Analysers can be
> suppressed. All three can be defeated — but not *accidentally*, and every defeat shows up in a
> diff. That is the actual goal: make boundary erosion **visible**, not impossible.

---

## 3. Module catalogue

| Module | Context | Class | Persistence style | Notes |
|---|---|---|---|---|
| `Ledger` | C7 | 🟡 | Event-sourced | The system of record |
| `Positions` | C8 | 🟡 | Projection over Ledger | Fully rebuildable |
| `Taxation` | C1 | 🔴 | **Stateless** | Pure function library + versioned rulesets as data |
| `Cashflow` | C9 | 🟡 | State + fitted models | Model versions retained |
| `Liabilities` | C10 | 🟡 | State + regenerated schedules | |
| `HumanCapital` | C11 | 🟡 | State | |
| `Goals` | C12 | 🟡 | Event-sourced | Policy history matters |
| `Twin` | C6 | 🔴 | Immutable snapshots | Content-addressed, blob + index |
| `Forecast` | C2 | 🔴 | Immutable artifacts | Blob + index; never deleted |
| `Attribution` | C3 | 🔴 | Derived, cached | Rebuildable from artifact pairs |
| `Calibration` | C4 | 🔴 | Append-only records | Matures over years |
| `Advisory` | C5 | 🔴 | Derived, short-lived | Options expire |
| `Reliability` | C13 | 🟡 | State + event-sourced incidents | |
| `Progression` | C14 | 🟡 | State | **Removable without affecting finance** |
| `Narrative` | C15 | 🟡 | Stateless | Generated text never persisted as fact |
| `Ingestion` | C16 | ⚪ | State + raw blob archive | ACL per source |
| `MarketData` | C17 | ⚪ | Time series + cache | Staleness-aware |
| `Identity` | C18 | ⚪ | State | Seam only |
| `Notification` | C19 | ⚪ | State | Consumes gated events only |

---

## 4. Schema isolation

Each module owns a **PostgreSQL schema**; no module reads another's tables.

```sql
CREATE SCHEMA ledger;      CREATE SCHEMA positions;   CREATE SCHEMA cashflow;
CREATE SCHEMA liabilities; CREATE SCHEMA humancap;    CREATE SCHEMA goals;
CREATE SCHEMA twin;        CREATE SCHEMA forecast;    CREATE SCHEMA attribution;
CREATE SCHEMA calibration; CREATE SCHEMA advisory;    CREATE SCHEMA reliability;
CREATE SCHEMA progression; CREATE SCHEMA ingestion;   CREATE SCHEMA marketdata;
CREATE SCHEMA identity;    CREATE SCHEMA notification;
CREATE SCHEMA shared;      -- outbox, migrations, tenancy registry only
```

**Enforced by database roles**, not just convention: each module's connection uses a role with
`USAGE` on its own schema plus `shared`. A cross-schema query fails at the database with a
permission error — the one enforcement mechanism a developer cannot suppress with an attribute.

Cross-module joins are therefore impossible. Where a join would have been convenient, the answer is
a **projection fed by events** — which is also what makes the read models rebuildable (AI-1).

Migrations are per-module, versioned independently, applied in dependency order by
`Atlas.Host.Migrator` at startup with an advisory lock.

---

## 5. Module lifecycle contract

Every module implements one registration interface, and the host discovers them:

```
IAtlasModule
  ├─ Name, Version
  ├─ RegisterServices(IServiceCollection, IConfiguration)
  ├─ RegisterEventHandlers(IEventBusBuilder)
  ├─ RegisterEndpoints(IEndpointRouteBuilder)      // minimal APIs, module-prefixed
  ├─ Migrations → IReadOnlyList<Migration>
  └─ HealthChecks → IReadOnlyList<IHealthCheck>
```

Consequences worth stating explicitly:
- **Adding a module is a one-line change** in the host. Removing one is deleting a project reference.
- `Progression` and `Narrative` can be removed entirely and the system must still pass every
  financial test (BR-804). A CI job named `no-frills-build` does exactly this on every PR — the
  cleanest possible proof that gamification and AI are genuinely peripheral.

---

## 6. The in-process event bus

```
IEventBus
  ├─ PublishAsync<TEvent>(TEvent, CancellationToken)      // via outbox, transactional
  └─ (subscription registered at startup, typed, per-module)
```

- Publication **enrols in the ambient transaction** by writing to `shared.outbox`.
- A background dispatcher reads the outbox and invokes handlers, at-least-once.
- Handlers must be idempotent (`BR-103` pattern applies generally).
- Failed handlers retry with exponential backoff, then land in `shared.outbox_dead_letter` and
  raise a **system** alert (never a user-facing one).

**Swap path to Service Bus:** implement `ServiceBusEventBus : IEventBus` and change one
registration. The outbox pattern is retained; only the transport changes. This is the entire cost
of the "microservices later" option, paid up front, once.

---

## 7. Anti-patterns explicitly banned

| Banned | Why | Instead |
|---|---|---|
| Shared "Common" or "Utils" project | The universal path to a big ball of mud | `Atlas.Kernel` only, ADR-gated |
| Cross-module database joins | Destroys module autonomy silently | Event-fed projections |
| Referencing another module's entity type | Couples internals; makes refactoring cross-cutting | Contracts DTOs |
| Calling another module's handler directly | Hidden synchronous coupling | Publish an event or use an OHS port |
| "Temporary" direct reference | Never temporary | ADR or don't |
| Generic repository over all aggregates | Leaks persistence into domain | Aggregate-specific ports |
| `DateTime.Now` in domain code | Destroys reproducibility (AI-2) | Time as a parameter |
| Anemic domain model | Pushes invariants into services where they are unenforceable | Behaviour on aggregates |

---

## 8. When to extract a module into a service

Pre-committed criteria, so the decision is engineering rather than fashion. Extract only when
**two or more** hold:

1. Materially different scaling profile (bursty CPU, sustained memory) — *`Forecast`/`atlas-sim` already meets this*
2. Materially different availability requirement
3. Independent deployment cadence genuinely needed
4. A different runtime or language is warranted (e.g. a Python numerical stack)
5. Team boundaries require it — *not applicable at N=1*

Criterion 4 is worth flagging: if the stochastic engine ever needs the Python scientific ecosystem,
`atlas-sim` is already a separate container consuming immutable snapshots and producing immutable
artifacts. **That extraction is a rewrite of one process, not of the system** — which is exactly
what the seam was for.

---

**See also:** [Architecture Vision](01-architecture-vision.md) · [Repository Structure](../05-engineering/01-repository-structure.md) · [ADR-0001](adr/ADR-0001-modular-monolith.md)
