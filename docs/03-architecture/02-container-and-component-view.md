# Container & Component View (C4)

**Status:** Ratified · **Owner:** Principal Architect · **Last reviewed:** 2026-08-01

---

## Level 1 — System context

```mermaid
graph TB
    U["👤 The Operator<br/>(single user)"]

    ATLAS["🛰 PROJECT ATLAS<br/>Financial Independence OS"]

    BANK["🏦 Banks / Open Finance<br/>(via aggregator)"]
    BROKER["📈 Brokers / B3<br/>(CSV, notas, API)"]
    MKT["📊 Market data<br/>(B3, BCB SGS, FX)"]
    IDX["📉 Economic indices<br/>(IPCA, SELIC, CDI)"]
    LLM["🤖 LLM provider<br/>(narration only)"]
    OBS["📡 Observability backend"]

    U -->|records facts, declares policy,<br/>reviews options| ATLAS
    ATLAS -->|change events, incidents,<br/>ranked options| U

    BANK -->|transactions, balances| ATLAS
    BROKER -->|trades, positions, distributions| ATLAS
    MKT -->|marks| ATLAS
    IDX -->|series| ATLAS
    ATLAS -->|validated FactSet only| LLM
    LLM -->|prose| ATLAS
    ATLAS -->|OTLP| OBS
```

**Boundaries that define the system:**
- Atlas **never initiates a financial transaction**. Read-only against the outside world (NG-03).
- The LLM sees a `FactSet`, never the ledger (BR-900, BR-906).
- Every inbound integration passes an ACL; no external schema reaches the domain.

---

## Level 2 — Containers

```mermaid
graph TB
    subgraph client["Client tier"]
        IOS["📱 atlas-ios<br/>Flutter · primary surface<br/>offline-capable"]
        WEB["🌐 atlas-web<br/>Flutter web · Static Web Apps"]
    end

    subgraph compute["Azure Container Apps · Consumption"]
        API["⚙️ atlas-api<br/>.NET 10 modular monolith<br/>min 0 / max 1"]
        SIM["🧮 atlas-sim<br/>Monte Carlo runner · CA Job<br/>scale-to-zero, ephemeral"]
    end

    subgraph data["Data tier"]
        PG[("🐘 PostgreSQL Flexible<br/>17 schemas · bitemporal ledger")]
        BLOB[("📦 Blob Storage<br/>snapshots · artifacts · raw · exports")]
        KV["🔐 Key Vault"]
    end

    IOS -->|HTTPS/JSON · OIDC| API
    WEB -->|HTTPS/JSON · OIDC| API
    API -->|SQL, per-module role| PG
    API -->|read/write artifacts| BLOB
    API -->|enqueue simulation| SIM
    SIM -->|read snapshot| BLOB
    SIM -->|write artifact| BLOB
    SIM -->|index artifact| PG
    API -->|managed identity| KV
    SIM -->|managed identity| KV
```

| Container | Tech | Responsibility | Scaling |
|---|---|---|---|
| `atlas-ios` | Flutter / Dart | Mission Control surface, local cache, offline capture | Device |
| `atlas-web` | Flutter web | Secondary surface, deep analysis views | Static CDN |
| `atlas-api` | .NET 10 | All 17 modules, command/query handling, projections, event dispatch | 0 → 1 |
| `atlas-sim` | .NET 10 Job | Path simulation only. Stateless: snapshot in → artifact out | 0 → N |
| PostgreSQL | PG 17 | Truth, intent, projections, indices | Vertical |
| Blob | StorageV2 | Immutable artifacts, raw payloads, exports | Unbounded |

**Why `atlas-sim` is separate.** Not modularity — it is already a module. It is separated because
its resource profile is genuinely different: bursty, CPU-saturating, minutes-long, and it must not
be able to starve the API of memory. It is also the natural extraction point if the numerical stack
ever moves to Python ([Modular Monolith §8](03-modular-monolith.md)).

---

## Level 3 — Components inside `atlas-api`

```mermaid
graph TB
    subgraph edge["Edge"]
        EP["Minimal API endpoints<br/>module-prefixed"]
        AUTH["AuthN/AuthZ · OIDC"]
        VAL["Request validation"]
    end

    subgraph loop1["① TRUTH LOOP"]
        ING["Ingestion<br/>ACL per source"]
        LED["Ledger<br/>event-sourced, bitemporal"]
        POS["Positions & Valuation"]
        MKTD["Market Data"]
        REC["Reconciliation"]
    end

    subgraph domain["Life model"]
        CF["Cashflow & Behaviour"]
        LIA["Liabilities"]
        HC["Human Capital"]
        GOL["Goals & Policy"]
        TAX["Taxation BR<br/>⚡ pure, stateless"]
    end

    subgraph loop2["② FORESIGHT LOOP"]
        TWIN["Digital Twin<br/>snapshot builder"]
        MAT["Materiality Gate"]
        FC["Forecast orchestrator"]
    end

    subgraph loop3["③ DECISION LOOP"]
        ATTR["Attribution<br/>Shapley decomposition"]
        GATE["🚦 Signal Gate"]
        REL["Reliability · SLI/SLO/Incident"]
        ADV["Advisory · policy space"]
    end

    subgraph loop4["④ CALIBRATION LOOP"]
        CAL["Calibration & scoring"]
    end

    subgraph peripheral["Peripheral · removable"]
        PROG["Progression"]
        NAR["Narrative"]
        NOTIF["Notification"]
    end

    BUS["🔄 IEventBus + transactional outbox"]

    EP --> AUTH --> VAL --> ING
    ING --> LED --> POS
    MKTD --> POS
    POS --> REC
    LED --> CF
    LED --> LIA
    CF --> HC
    POS --> TWIN
    CF --> TWIN
    LIA --> TWIN
    HC --> TWIN
    GOL --> TWIN
    TAX -.pure calls.-> TWIN
    TWIN --> MAT --> FC
    FC --> ATTR --> GATE
    GATE --> REL
    ATTR --> ADV
    TAX -.pure calls.-> ADV
    FC --> CAL
    CAL -->|gates| ADV
    REL --> NOTIF
    GATE --> NOTIF
    ADV --> NAR
    GOL --> PROG
    CF --> PROG
    BUS -.- LED
    BUS -.- TWIN
    BUS -.- ATTR
    BUS -.- REL
```

### Component responsibilities

| Component | Single responsibility | Must not |
|---|---|---|
| Ingestion | Translate external formats into `EntryProposal` | Post to the ledger directly |
| Ledger | Record balanced, bitemporal truth | Value anything, interpret anything |
| Positions | Maintain lots, basis, corporate actions | Apply tax rules |
| Taxation | Compute tax consequences, purely | Hold state, read a clock, do I/O |
| Twin | Assemble a complete, hashed snapshot | Compute forecasts |
| Materiality Gate | Decide whether a state change warrants re-forecast | Decide what the user sees |
| Forecast | Produce immutable artifacts | Compare artifacts, interpret them |
| Attribution | Decompose deltas into classified drivers | Decide user visibility alone |
| **Signal Gate** | Decide what reaches the user | Compute anything |
| Reliability | Evaluate SLIs, manage incidents | Alert on stochastic drivers |
| Advisory | Enumerate and rank policy options | Instruct, or name securities |
| Calibration | Score past forecasts, gate advice | Auto-retune models |
| Progression | Track process adherence | Observe returns or valuations |
| Narrative | Render a `FactSet` as prose | Compute, round, or convert |

**The two gates are the architecture's most important components.** The Materiality Gate controls
*compute cost*; the Signal Gate controls *user attention*. Both are cheap code protecting expensive
resources.

---

## Level 4 — The critical path, sequenced

```mermaid
sequenceDiagram
    participant U as Operator
    participant API as atlas-api
    participant LED as Ledger
    participant TWIN as Twin
    participant SIM as atlas-sim
    participant ATTR as Attribution
    participant GATE as Signal Gate
    participant N as Notification

    U->>API: import broker statement
    API->>API: ACL parse → EntryProposal[]
    API->>LED: PostJournalEntry (idempotent, bitemporal)
    LED-->>API: JournalEntryPosted
    API->>TWIN: rebuild TwinState
    TWIN->>TWIN: MaterialityGate — does this move t_FI?

    alt material
        TWIN->>TWIN: TwinSnapshot (content-addressed)
        TWIN->>SIM: enqueue forecast(snapshotHash, modelVersion, seed)
        SIM->>SIM: simulate N paths, after-tax, real terms
        SIM-->>API: ForecastArtifact stored
        API->>ATTR: attribute(prior, current)
        ATTR->>ATTR: decompose → Controllable / Structural / Stochastic
        ATTR->>GATE: Delta + Attribution
        alt passes materiality AND significance AND dedup
            GATE->>N: ChangeEvent
            N->>U: "Your FI date moved 41 days. Driver: salary change."
        else fails gate
            GATE->>GATE: SignalGateSuppressed — retained, silent
        end
    else immaterial
        TWIN->>TWIN: defer to daily tick
    end
```

**Read the failure branches, not the happy path.** Two of the three terminal states are *silence*.
That is the design working: the system's default output is nothing, and speech is earned.

---

## Deployment view

| Unit | Artifact | Trigger | Rollback |
|---|---|---|---|
| `atlas-api` | OCI image (GHCR) | Push to `main`, gates green | Revision revert, < 5 min |
| `atlas-sim` | OCI image (GHCR) | Same | Job version pin |
| `atlas-web` | Static bundle | Same | SWA revision |
| `atlas-ios` | TestFlight build | Tag | Previous build |
| Database | Bicep + module migrations | Deploy-time, advisory-locked | Expand/contract only — never a destructive down-migration |

---

**See also:** [Architecture Vision](01-architecture-vision.md) · [Modular Monolith](03-modular-monolith.md) · [DevOps & CI/CD](09-devops-and-cicd.md)
