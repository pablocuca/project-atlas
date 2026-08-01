# Ingestion & Integration

**Status:** Ratified · **Owner:** Principal Architect · **Last reviewed:** 2026-08-01

> **The strategic point of this document:** the hard part of Atlas is not the mathematics — it is
> getting trustworthy data in and keeping it trustworthy. Most personal-finance projects die here,
> waiting on an integration that never lands.

---

## 1. The manual-first doctrine

**Atlas must be fully useful with zero integrations.** Manual entry is not a fallback; it is
`Source #1`, subject to the same invariants, the same reconciliation, and the same idempotency as
any automated feed (BR-109).

Rationale:
1. **Never blocked.** Open Finance Brasil does not grant personal direct access — participation is
   regulated, and aggregators are commercial contracts. A design that depends on them has an
   external dependency on its critical path from day one.
2. **Correctness first.** Building the ledger, tax engine, and forecast against hand-entered data
   forces the domain to be right before volume hides the errors.
3. **Graceful degradation forever.** Any source can break — a broker changes a CSV column, an
   aggregator drops a bank. Manual entry means the system degrades to slower, never to broken.
4. **Sovereignty.** The user is never locked out of their own system by a third party's pricing or
   API decisions.

**Consequence for the roadmap:** M0–M3 ship with manual + CSV only. Aggregator integration is
**M5 or later**, and is explicitly an *efficiency* feature, not a *capability* feature.

---

## 2. Source taxonomy

| Tier | Source | Effort | Reliability | Milestone |
|---|---|---|---|---|
| 1 | Manual entry (UI, mobile quick-capture) | — | Perfect if disciplined | M0 |
| 1 | CSV / OFX import (bank + broker exports) | Low | High | M1 |
| 2 | Broker *nota de corretagem* PDF parsing | Medium | Medium — layout drift | M4 |
| 2 | B3 CEI / Área do Investidor export | Medium | Medium | M4 |
| 2 | Public market data (B3 quotes, BCB SGS for IPCA/SELIC/CDI) | Low | High | M2 |
| 3 | Aggregator (Pluggy / Belvo) — Open Finance | High + **paid** | High | M5+ |
| 3 | Direct Open Finance participation | Prohibitive — regulated | — | Not planned |

**On aggregator cost.** A commercial aggregator will likely exceed the entire infrastructure budget
([ADR-0015](adr/ADR-0015-cost-ceiling.md)). It is therefore evaluated separately, on its own
cost/benefit, and its absence must never degrade correctness — only convenience. Recorded as
`RISK-006`.

---

## 3. The ingestion pipeline

```
        ┌──────────────────────────────────────────────────────────────┐
        │  1. CAPTURE — raw payload → blob, unmodified, forever        │
        │     idempotency key computed here, before any interpretation │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  2. PARSE — source-specific ACL → ParsedRow[]                │
        │     failures are recorded per row, never fatal to the batch  │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  3. NORMALISE — external schema → Atlas vocabulary           │
        │     Money, Commodity, ValidTime. No external type escapes    │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  4. DEDUPLICATE — against idempotency keys AND fuzzy match   │
        │     overlapping statement windows are the normal case        │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  5. PROPOSE — EntryProposal[] with confidence + rationale    │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  6. CONFIRM — auto (high confidence, low value) or human     │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  7. POST — balanced, bitemporal journal entries              │
        └────────────────────────────┬─────────────────────────────────┘
        ┌────────────────────────────▼─────────────────────────────────┐
        │  8. RECONCILE — source-reported balance vs ledger-derived    │
        │     discrepancy > tolerance ⇒ data-quality SLI breach        │
        └──────────────────────────────────────────────────────────────┘
```

**Stage 1 is the most important stage.** The raw payload is archived *before* parsing, compressed,
forever. Every parser bug discovered in 2031 can be fixed and **replayed against 2026 data**. This
costs almost nothing (a few MB per year) and makes parser defects recoverable rather than
permanent. A parse failure loses time; it never loses information.

---

## 4. Idempotency

```
idempotencyKey = SHA-256( sourceId ‖ canonical(rawRecord) )
```

Computed from the **raw record**, before normalisation, so that changes to Atlas's own parsing never
alter the key of previously-imported data. Unique per `(tenantId, sourceId)` (BR-103).

**Fuzzy duplicate detection** additionally runs across sources — the same PIX transfer may appear in
both a bank feed and a manual entry. Candidate matches on `(date ± 2 days, amount exact,
counterparty similarity ≥ 0.85)` are flagged for human resolution and **never auto-merged**. Silent
merging of financial records is a class of bug that is undetectable after the fact, so it is
prohibited outright.

---

## 5. Anti-corruption layers

Every source implements one interface:

```
ISourceAdapter
  ├─ SourceKind, SchemaVersion
  ├─ Sniff(payload)              → confidence that this adapter applies
  ├─ Parse(payload)              → ParsedRow[] + ParseFailure[]
  ├─ Normalise(ParsedRow)        → NormalisedRecord      // Atlas vocabulary only
  ├─ ProposeEntries(records)     → EntryProposal[]
  └─ ExpectedBalance(payload)?   → Money                 // for reconciliation
```

Non-negotiable rules:

| Rule | Reason |
|---|---|
| No external type crosses into `Ledger` | External schemas are the primary vector of domain rot |
| Every adapter is versioned; old versions retained | Replaying 2026 payloads must use the 2026 parser |
| Every adapter has a **golden-file corpus** of real (redacted) payloads | Layout drift is detected by CI, not by wrong numbers |
| An adapter never writes to the ledger | It proposes; the Ledger module decides |
| Unknown fields are preserved in the proposal metadata | Tomorrow's requirement is often in today's ignored column |

---

## 6. Classification and the human in the loop

Auto-confirmation is permitted only when **all** hold:
- adapter confidence ≥ 0.95, **and**
- the transaction matches an established recurring pattern, **and**
- value ≤ the auto-confirm ceiling (default R$ 500), **and**
- it is not an investment transaction (trades always require confirmation — cost basis is
  unforgiving), **and**
- it does not affect the Essential/Discretionary classification of a new category.

Everything else queues for review. **Category classification is a user decision, versioned, with an
audit trail** (INV-060) — because Essential vs Discretionary moves the FI number directly, and a
silent reclassification would show up as an unexplained change in the North Star.

---

## 7. Market data

| Series | Source | Cadence | Staleness tolerance |
|---|---|---|---|
| Equity / FII / ETF quotes | B3 public data | Daily close | 48 h |
| IPCA, SELIC, CDI, IGP-M | BCB SGS API (free, public) | On publication | 45 days |
| Treasury curve | Tesouro Direto public files | Daily | 72 h |
| FX (USD/BRL, EUR/BRL) | BCB PTAX | Daily | 48 h |
| Fund quotas | CVM public data | Daily | 5 days |

Posture:
- **Cache-first.** Every observation is stored on arrival; the system never needs a live call to
  produce a valuation.
- **Staleness is data, not an error.** Every mark carries its age; every valuation carries the
  worst staleness it used (INV-044).
- **A stale mark never blocks a forecast.** The forecast runs and is labelled `Degraded` (BR-311).
  Refusing to answer is a worse failure than answering with a stated caveat.
- **No paid market data.** Free public Brazilian sources are sufficient for daily-close valuation,
  and NG-06 removes any need for intraday.

---

## 8. Failure handling

| Failure | Response | User impact |
|---|---|---|
| Payload unparseable | Archive raw, record failure, notify with the specific row | Manual entry available |
| Partial parse | Post what parsed; queue failures separately | Partial value delivered |
| Source unreachable | Exponential backoff, use cached data, raise freshness SLI | Degraded label |
| Reconciliation gap > tolerance | Data-quality incident with a runbook | Explicit, actionable |
| Duplicate suspected | Queue for resolution, **never auto-merge** | One decision required |
| Adapter schema drift | Golden-file test fails in CI **before** production impact | None, if caught |

Guiding principle: **never lose an input, never block the user, never silently guess.**

---

**See also:** [Data Strategy](04-data-strategy.md) · [Business Rules BR-1xx](../02-domain/05-business-rules.md) · [Risk Register](../06-governance/01-risk-register.md)
