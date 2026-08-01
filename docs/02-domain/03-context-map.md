# Context Map

**Status:** Ratified · **Owner:** Domain / CTO · **Last reviewed:** 2026-08-01

How the seventeen contexts relate, in DDD strategic patterns. The map is the contract: a
relationship not listed here does not exist, and creating one requires an ADR.

---

## Pattern legend

| Pattern | Meaning in Atlas |
|---|---|
| **PL** — Published Language | A versioned, documented contract; breaking changes require a major version |
| **OHS** — Open Host Service | Stable interface offered to many consumers |
| **ACL** — Anti-Corruption Layer | Translation shield; upstream model never leaks through |
| **CF** — Conformist | Downstream accepts upstream's model as-is (only tolerated for Generic contexts) |
| **SK** — Shared Kernel | A small, jointly-owned model. **Exactly one exists** (see §4) |
| **U/D** — Upstream / Downstream | Direction of influence |
| **P** — Partnership | Two contexts must change together; a design smell to be minimised |

---

## 1. The relationship table

| # | Upstream | Downstream | Pattern | Contract |
|---|---|---|---|---|
| R01 | External systems (banks, brokers, B3, aggregators, CSV) | Ingestion | **ACL** | Adapter-specific, versioned per source |
| R02 | Ingestion | Ledger | **PL** | `EntryProposal` → `PostJournalEntry`. Never raw source rows |
| R03 | Ledger | Position & Valuation | **PL / OHS** | `JournalEntryPosted` stream + `LedgerBalanceQuery` |
| R04 | Market Data | Position & Valuation | **ACL** | `MarkObservation` with mandatory staleness metadata |
| R05 | Market Data | Forecast | **ACL** | `IndexSeries` (IPCA, SELIC, CDI, IBOV, IFIX) |
| R06 | Ledger | Cashflow & Behaviour | **PL** | Income/Expense postings + category assignments |
| R07 | Ledger | Liabilities | **PL** | Instalment postings reconciled to schedule |
| R08 | Cashflow & Behaviour | Human Capital | **PL** | Realised earnings series |
| R09 | Position & Valuation | Taxation | **PL** | `TaxLotView` — basis, dates, instrument class |
| R10 | Taxation | Position & Valuation, Forecast, Advisory | **OHS, pure fn** | `ITaxJurisdiction`. Stateless, deterministic |
| R11 | Position & Valuation | Digital Twin | **PL** | `PositionBookSnapshot` |
| R12 | Cashflow & Behaviour | Digital Twin | **PL** | `SpendingModelSnapshot`, `IncomeModelSnapshot` |
| R13 | Liabilities | Digital Twin | **PL** | `LiabilitySchedule` |
| R14 | Human Capital | Digital Twin | **PL** | `HumanCapitalSnapshot` incl. market correlation |
| R15 | Goals & Policy | Digital Twin | **PL** | `PolicySnapshot`, `GoalSet`, `ConfidenceTarget` |
| R16 | Digital Twin | Forecast | **PL, immutable** | `TwinSnapshot` (content-addressed). **The single most important contract in the system** |
| R17 | Forecast | Attribution | **PL, immutable** | `ForecastArtifact` |
| R18 | Digital Twin | Attribution | **PL** | Snapshot pair `(t₀, t₁)` for driver decomposition |
| R19 | Attribution | Financial Reliability | **PL** | `ChangeEvent` with `DriverClass` |
| R20 | Attribution | Advisory | **PL** | `DriverRanking` — where to look for leverage |
| R21 | Forecast | Advisory | **OHS** | `IForecastRunner` for counterfactual evaluation |
| R22 | Taxation | Advisory | **OHS, pure fn** | Option cost must be after-tax |
| R23 | Forecast | Calibration | **PL** | Every `ForecastArtifact` is registered for future scoring |
| R24 | Ledger + Market Data | Calibration | **PL** | Realised outcomes for scoring |
| R25 | Calibration | Advisory | **PL, gating** | `ReliabilityStatus`. **Degraded reliability suppresses advice** |
| R26 | Calibration | Forecast | **PL, feedback** | `DriftSignal` → triggers model review, never auto-retunes |
| R27 | Financial Reliability | Notification | **PL** | `IncidentDeclared`, `BurnRateAlert` |
| R28 | Attribution | Notification | **PL, gated** | Only post-Signal-Gate `ChangeEvent` |
| R29 | Goals & Policy | Progression | **PL** | Declared commitments to measure adherence against |
| R30 | Cashflow & Behaviour | Progression | **PL** | Realised process actions only |
| R31 | Attribution, Advisory, Reliability | Narrative | **PL, one-way** | `FactSet`. Narrative can read nothing else |
| R32 | Identity | all | **SK** (see §4) | `TenantId`, `PrincipalId` |
| R33 | Advisory | Goals & Policy | **PL** | `OptionAccepted` → `PolicyDeclared` — closes intention/action loop |

---

## 2. The critical seams

Three relationships carry disproportionate risk. They get explicit tests and explicit ADRs.

### Seam A — `TwinSnapshot` (R16)
Everything the future depends on passes through one immutable object. Requirements:

- **Content-addressed.** `hash = SHA-256(canonical serialisation)`. Two identical financial
  situations produce one hash.
- **Complete.** A forecast must be reproducible from the snapshot alone, with zero additional
  reads. If the engine needs a fact not in the snapshot, that is a defect, not a lookup.
- **Versioned schema.** Old snapshots must remain deserialisable for the life of the project —
  this is what makes decade-scale replay possible.

### Seam B — `ITaxJurisdiction` (R10, R22)
The tax context is a **pure, stateless, deterministic function library** behind one interface.

```
ITaxJurisdiction
  ├─ ClassifyLot(instrument, acquisition) → TaxRegime
  ├─ ComputeDisposal(lots, disposal, date, yearContext) → TaxConsequence
  ├─ ComputePeriodicWithholding(lots, date) → TaxConsequence   // come-cotas
  ├─ ComputeIncomeTax(incomeEvents, yearContext) → TaxConsequence
  ├─ ProjectTaxDrag(positions, policy, horizon) → TaxDragCurve
  └─ RulesetVersion → SemVer + effective date range
```

Why this shape: Brazilian tax law changes. A **versioned ruleset with effective dates** lets Atlas
compute 2027 taxes under 2027 rules while still replaying a 2026 forecast under 2026 rules —
without which historical forecasts become uninterpretable and calibration breaks.

### Seam C — Calibration → Advisory gate (R25)
The only relationship in the system with **veto power**. When forecast reliability for a horizon
band falls below its SLO, Advisory must suppress options whose ranking depends on that band, and
say so explicitly. This is enforced in the Advisory module's own invariants, not by discipline.

---

## 3. Conformist relationships — deliberately tolerated

| Relationship | Why conformity is acceptable |
|---|---|
| Notification → dispatch providers | Provider model is trivial and swappable |
| Identity → Entra ID / passkey provider | Standards-based; no domain semantics to protect |

Nothing else may be Conformist. Every financial data source gets an ACL, without exception —
external schemas are the primary vector by which a domain model rots.

---

## 4. The one Shared Kernel

Exactly one shared kernel exists: **`Atlas.Kernel`**.

| Contents | Rationale |
|---|---|
| `Money` (integer minor units + `Commodity`) | Duplicating money types across contexts guarantees rounding divergence |
| `Commodity` | Same |
| `ValidTime`, `DecisionTime`, `BitemporalInterval` | Bitemporality is a system-wide invariant |
| `TenantId`, `PrincipalId` | Threaded everywhere by [ADR-0011](../03-architecture/adr/ADR-0011-single-tenant-core.md) |
| `Percentage`, `Rate`, `Duration`, `DateRange` | Primitive-obsession guards |
| Domain event base contracts | Required for the event bus to be typed |

**Kernel rules, enforced by review and build:**
- The kernel contains **no business logic** beyond value-object invariants and arithmetic.
- The kernel depends on **nothing** — not even the BCL beyond primitives.
- Adding a type to the kernel requires an ADR. The kernel growing is the classic path by which a
  modular monolith becomes a big ball of mud.

---

## 5. Contract governance

| Rule | Detail |
|---|---|
| Versioning | Published Languages use SemVer. Breaking change ⇒ major ⇒ ADR |
| Compatibility window | Two major versions of any snapshot/artifact schema must remain readable, forever for `TwinSnapshot` and `ForecastArtifact` |
| Testing | Every PL relationship has **contract tests** run by both sides in CI |
| Documentation | Every PL has a schema file in `contracts/` with generated docs |
| Deprecation | Announce → dual-write → migrate → remove. Never remove in one step |

---

**See also:** [Bounded Contexts](02-bounded-contexts.md) · [Modular Monolith](../03-architecture/03-modular-monolith.md) · [Domain Model](04-domain-model.md)
