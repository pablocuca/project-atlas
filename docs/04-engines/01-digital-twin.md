# Digital Twin

**Status:** Ratified · **Owner:** Principal Architect / Domain · **Context:** C6 (Core)

> The Digital Twin is the complete, consistent, versioned model of a financial life at an instant.
> It is the **integration surface** of the entire system: everything below it produces facts,
> everything above it consumes a snapshot.

---

## 1. What the Twin is, and is not

| The Twin **is** | The Twin **is not** |
|---|---|
| A consistency point across 8 contexts at one instant | A database or a cache |
| The complete input contract of every forecast | A place where business logic lives |
| Content-addressed and immutable once taken | Mutable state to be edited |
| Sufficient on its own to reproduce any forecast | A partial view requiring further lookups |

**The defining property is completeness (INV-110).** If a forecast engine needs a fact that is not
in the snapshot, that is a defect in the snapshot, never a lookup to be added. This single rule is
what makes reproducibility (AI-2) achievable — because a snapshot that is complete today is
complete in 2046 too.

---

## 2. Composition

```
TwinSnapshot
├─ identity
│   ├─ hash                SHA-256 of canonical serialisation — the identity
│   ├─ takenAt             UTC instant
│   ├─ schemaVersion       monotonic integer
│   └─ tenantId
├─ positionBook            from C8 — lots, basis, quantities, instrument classes
├─ marketState             from C17 — marks, curves, indices, and their staleness
├─ spendingModel           from C9 — fitted process, floor, personal inflation
├─ incomeModel             from C9 — streams, seasonality, 13th, bonus distribution
├─ liabilitySchedules      from C10 — amortisation, indexation, reset dates
├─ humanCapital            from C11 — trajectory, hazard rate, marketBeta
├─ policy                  from C12 — π₀: savings, allocation, sequencing, wrappers
├─ goals                   from C12 — priorities, amounts, dates, flexibility
├─ taxState                from C1 — YTD realised gains, exemption consumption, carry-forwards
└─ provenance
    ├─ sourceRefs[]        which ledger decision-time this reflects
    ├─ coverage            % of net worth in reconciled sources
    ├─ freshness           worst staleness across all inputs
    └─ degradations[]      explicit list of known quality issues
```

### Why `provenance` is mandatory
A forecast built on 60%-covered, 5-day-stale data is a different epistemic object than one built on
98%-covered, same-day data. Both are legitimate; conflating them is not. Provenance travels with
the snapshot so every downstream output can be labelled `Degraded` honestly (Law 8) rather than
presented as clean.

---

## 3. Snapshot construction

```
1. ACQUIRE       Read consistency point: fix (validTime, decisionTime) coordinates
2. GATHER        Each of 8 contexts renders its section at those coordinates
3. VALIDATE      Completeness, internal consistency, unit correctness
4. CANONICALISE  Deterministic serialisation
5. HASH          SHA-256 → identity
6. DEDUPE        If hash exists, return the existing snapshot — no duplicate
7. STORE         Blob (compressed) + Postgres index row
8. EMIT          TwinSnapshotTaken
```

### Consistency point
All eight sections are rendered at **one fixed bitemporal coordinate pair**, not at "now" as each
module happens to be queried. Otherwise the position book could reflect a trade that the tax state
does not, producing an internally inconsistent snapshot — a defect that would be invisible until it
produced a wrong forecast months later.

### Canonicalisation (the load-bearing detail)
Keys sorted lexicographically · no insignificant whitespace · numbers as decimal strings, never
IEEE-754 output · times as UTC ISO-8601 with fixed precision · explicit nulls · no map iteration
order dependence.

A CI golden test asserts that identical financial state hashes identically **across machines,
runtimes, and .NET versions**. Without that test, AI-2 fails silently and nobody discovers it until
the calibration record is years deep and worthless.

### Deduplication
Step 6 is a real optimisation: nothing meaningful changes on most days. Identical state produces an
identical hash, and the existing snapshot and its forecast are reused. Storage and compute both
benefit, and — more importantly — "nothing changed" becomes a *provable* statement rather than an
inference.

---

## 4. The Materiality Gate

Re-forecasting on every ledger posting is wasteful and, worse, generates spurious deltas that the
Signal Gate must then discard (hotspot H3).

```
                    state change
                         │
              ┌──────────▼──────────┐
              │  Cheap sensitivity  │   analytic first-order estimate of ∂t_FI/∂x
              │  estimate           │   no simulation required
              └──────────┬──────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
   |Δt_FI| ≥ 7d    class = Structural   otherwise
   or Δ|assets|      or Policy change      │
   ≥ 2% of NW           │                  │
        │                │                  ▼
        └────────┬───────┘            defer to daily tick
                 ▼
        immediate re-forecast
```

**Design note.** The gate uses a *cheap analytic estimate*, never a simulation, to decide whether a
simulation is warranted. A gate that costs as much as the thing it guards is not a gate. First-order
sensitivities (`∂t_FI/∂savings_rate`, `∂t_FI/∂portfolio_value`, `∂t_FI/∂spending_floor`) are
computed once per forecast and cached on the artifact, making the gate essentially free.

**Always immediate, regardless of magnitude:** policy change, employment event, goal change,
tax-regime-affecting transaction, user-requested recompute.

---

## 5. Twin state vs Twin snapshot

| | `TwinState` | `TwinSnapshot` |
|---|---|---|
| Mutability | Continuously rebuilt | Immutable |
| Storage | Postgres projection | Blob, content-addressed |
| Purpose | Current view for the UI | Forecast input, forever |
| Rebuildable | Yes, from ledger (AI-1) | Never regenerated — retrieved |
| Lifetime | Disposable | Permanent |

`TwinState` is a projection. `TwinSnapshot` is a historical statement. Conflating them is the most
likely design error in this context — and it would silently destroy replay, because a "snapshot"
that is regenerated from current data is not a snapshot at all.

---

## 6. Schema evolution

Snapshot schema versions are monotonic integers, and **every reader is retained forever**
(INV-113, BR-A03).

| Change | Allowed? | Handling |
|---|---|---|
| Add optional section | Yes | Old readers ignore; new readers default it |
| Add required field | Yes, with a documented default | Reader supplies the historical default |
| Remove a field | No | Deprecate, stop populating, keep reading |
| Change a unit or semantic | **No** | New field, new name. Silent semantic change is unforgivable — it makes historical figures wrong without any signal |
| Change canonicalisation | Requires a new schema version and an ADR | It changes every hash |

The historical-corpus CI test deserialises one snapshot of every version ever released. Deleting a
reader breaks the build.

---

## 7. Human capital: the correlation that matters

`humanCapital` carries a **mandatory** `marketBeta` (INV-080) — the correlation between income
shocks and equity drawdowns.

The reason it is mandatory rather than optional: modelling human capital as uncorrelated with
markets is the standard simplification, and it hides the dominant tail risk in almost every real
financial life. Job loss and market crashes are **positively correlated** — the user is most likely
to need the portfolio precisely when it is most depressed and their income has stopped. A model
that misses this systematically understates ruin probability, and it does so in the exact scenarios
that matter.

Making the field required forces zero to be a *deliberate, recorded choice* rather than an
accidental default.

---

## 8. Performance and cost

| Property | Target | Note |
|---|---|---|
| Snapshot build time | ≤ 2 s p95 | 8 context queries + canonicalisation |
| Snapshot size | 50–200 KB raw, 10–40 KB zstd | |
| Snapshots/year | ≤ 365 (deduplicated, often far fewer) | |
| 40-year total | < 1 GB | Retention is effectively free |
| Materiality gate | ≤ 50 ms | Analytic only, no simulation |

---

## 9. Failure modes

| Failure | Response |
|---|---|
| A context cannot render its section | **Abort.** No partial snapshots — incompleteness would break INV-110 silently |
| Marks are stale | Proceed; record in `provenance.degradations`; downstream labels `Degraded` |
| Coverage below threshold | Proceed; record; the Coverage SLI breaches separately |
| Hash collision with different content | Impossible in practice (SHA-256); asserted anyway, and would be SEV-1 |
| Canonicalisation non-determinism | SEV-1. Blocks release. Every historical comparison is suspect until resolved |

Note the asymmetry: **incompleteness aborts, degradation proceeds.** An incomplete snapshot is
structurally invalid; a degraded one is a valid statement about imperfect information.

---

**See also:** [Forecast Engine](02-forecast-engine.md) · [Context Map Seam A](../02-domain/03-context-map.md) · [ADR-0006](../03-architecture/adr/ADR-0006-immutable-forecast-artifacts.md)
