# Data Strategy

**Status:** Ratified · **Owner:** Principal Architect · **Last reviewed:** 2026-08-01

The data outlives the code, the platform, and probably the language. This document specifies how.

---

## 1. Data classes and their guarantees

| Class | Examples | Mutability | Retention | Store |
|---|---|---|---|---|
| **Truth** | Journal entries, postings, accounts | Append-only, bitemporal | **Forever, no deletion path** | Postgres |
| **Immutable artifact** | Twin snapshots, forecast artifacts | Write-once | **Forever** | Blob + Postgres index |
| **Declared intent** | Policies, goals, classifications | Versioned, never overwritten | Forever | Postgres (event-sourced) |
| **Fitted model** | Spending model, human capital valuation | New version per refit | Forever (small) | Postgres |
| **Observation** | Marks, index series, FX | Append-only | 10y hot, then cold blob | Postgres + blob |
| **Projection** | Balances, position book, dashboards | **Disposable** | Rebuildable at any time | Postgres |
| **Derived analysis** | Attributions, options, narratives | Disposable / expiring | Cache lifetime | Postgres |
| **Raw payload** | Original CSV/OFX/API responses | Write-once | Forever (compressed) | Blob |

**The load-bearing distinction:** *Truth*, *artifact*, and *intent* can never be regenerated and are
therefore protected absolutely. Everything else is a cache. This is what makes schema evolution of
read models a non-event and backup strategy tractable.

---

## 2. The bitemporal ledger

### 2.1 Why bitemporality is non-negotiable

A broker corrects a trade from three weeks ago. Three questions must all remain answerable:

1. What is my position **today**, with everything we now know?
2. What did we **believe** my position was last Tuesday, when we told the user something?
3. What **actually** was my position last Tuesday, in hindsight?

Uni-temporal storage answers at most two. Without all three, "what changed since yesterday" silently
conflates *the world changed* with *we found out we were wrong* — and every delta the product
shows becomes untrustworthy. This is the single most common structural defect in personal finance
software.

### 2.2 Shape

```sql
CREATE TABLE ledger.journal_entry (
  entry_id        uuid PRIMARY KEY,
  tenant_id       uuid NOT NULL,
  valid_time      date        NOT NULL,   -- when it was true in the world
  decision_time   timestamptz NOT NULL,   -- when Atlas learned it
  decision_to     timestamptz NOT NULL DEFAULT 'infinity',
  corrects_entry  uuid REFERENCES ledger.journal_entry(entry_id),
  idempotency_key text NOT NULL,
  source_id       uuid NOT NULL,
  description     text NOT NULL,
  UNIQUE (tenant_id, source_id, idempotency_key)
);

CREATE TABLE ledger.posting (
  posting_id  bigserial PRIMARY KEY,
  entry_id    uuid NOT NULL REFERENCES ledger.journal_entry(entry_id),
  account_id  uuid NOT NULL,
  commodity   text NOT NULL,
  minor_units bigint NOT NULL,            -- signed; + debit, − credit
  lot_ref     uuid
);
```

**Design notes that matter:**
- `minor_units bigint`, signed. **No `numeric`, no `decimal`, no `money` type.** Direction is the
  sign; entry balance is `SUM(minor_units) = 0 GROUP BY commodity` — a constraint the database
  itself can verify with a deferred trigger.
- `decision_to` implements logical retraction *of a belief*. A restated fact closes the old
  belief interval and opens a new one. The row is never updated in the semantic sense, only its
  belief interval is closed.
- `UNIQUE (tenant_id, source_id, idempotency_key)` is the entire duplicate-import defence (BR-103).

### 2.3 Querying

Every balance query takes both coordinates. There is deliberately **no single-time overload** in
the API (INV-035) — the affordance to write the wrong query does not exist.

```sql
-- Position as believed at decision time D, for the world as of valid time V
SELECT commodity, SUM(minor_units)
FROM ledger.posting p JOIN ledger.journal_entry e USING (entry_id)
WHERE e.tenant_id = $tenant AND p.account_id = $account
  AND e.valid_time <= $V
  AND e.decision_time <= $D AND e.decision_to > $D
GROUP BY commodity;
```

Indexes: `(tenant_id, account_id, valid_time)` and `(tenant_id, decision_time, decision_to)`, plus a
BRIN index on `decision_time` — the table is naturally append-ordered, and BRIN costs almost nothing.

### 2.4 Performance posture

A single human generates roughly 10²–10³ entries per month; over 40 years, ~10⁵–10⁶ rows. **This
is a small table.** Balance queries run against precomputed daily snapshots in
`ledger.balance_daily` (a projection, therefore disposable), with the bitemporal query used for
audit, as-of reconstruction, and projection rebuild.

Stating this explicitly matters: it licenses choosing **correctness and auditability over query
performance** everywhere in the ledger, because the data volume never makes that trade painful.

---

## 3. Content-addressed artifacts

Twin snapshots and forecast artifacts are content-addressed:

```
hash = SHA-256( canonical_json(payload) )
blob path: {tenant}/{kind}/{hash[0:2]}/{hash}.json.zst
```

**Canonical JSON:** keys sorted lexicographically, no insignificant whitespace, numbers as decimal
strings (never IEEE-754 output), all times UTC ISO-8601 with explicit precision, explicit nulls.
A canonicalisation golden test runs in CI: identical financial state must hash identically **on any
machine, on any runtime version, in any year**. Without that, AI-2 quietly fails and nobody notices
until calibration is years deep.

Postgres holds only an index row (`hash`, `taken_at`, `schema_version`, `tenant_id`, metadata) —
blob storage is a dumb byte store, deliberately, so exiting Azure is a copy operation.

### Compression and cost
zstd level 10. A snapshot is ~50–200 KB raw, ~10–40 KB compressed. At one snapshot per day for 40
years: well under 1 GB total. **Retention is free; deletion is therefore never justified by cost** —
a fact worth writing down so it is never re-litigated.

---

## 4. Schema evolution

| Data class | Evolution rule |
|---|---|
| Truth (ledger) | **Additive only.** Never drop or repurpose a column. New concepts get new tables |
| Artifacts | Versioned schema; **every historical reader is retained forever** (INV-113, BR-A03) |
| Intent | Additive; new policy dimensions default to the prior implicit behaviour |
| Projections | Free to change — drop and rebuild |

**The historical-corpus test.** A fixture directory holds one snapshot and one artifact per schema
version ever released. CI deserialises all of them on every build. Deleting a reader breaks the
build. This is the mechanism that makes "readable in 2046" an engineering fact rather than an
aspiration.

---

## 5. Portability and exit

`atlas export --full --out ./export` produces:

```
export/
  MANIFEST.json          schema versions, hashes, tool version, export timestamp
  SCHEMA.md              human-readable description of every file and unit
  ledger/                *.parquet  entries, postings, accounts (+ CSV mirror)
  snapshots/             *.json.zst content-addressed, path = hash
  forecasts/             *.json.zst
  calibration/           *.parquet
  intent/                *.json     policies, goals, classifications, full history
  marketdata/            *.parquet
  raw/                   original source payloads, unmodified
```

Guarantees (BR-A00, BR-A01):
- **Self-describing.** `SCHEMA.md` states every field, unit, and time semantic. A stranger with no
  access to the code can interpret the data.
- **Open formats only.** Parquet, JSON, CSV, zstd. No proprietary serialisation anywhere.
- **CI-verified.** The pipeline runs a full export **and re-imports it into a clean database**,
  asserting ledger and artifact equality. An export that has never been restored is not a backup.

---

## 6. Backup and durability

| Layer | Mechanism | RPO | RTO |
|---|---|---|---|
| Postgres | Azure automated backups, 7-day PITR | 5 min | ~1 h |
| Postgres (owned) | Nightly `pg_dump` → blob, cool tier, 90-day retention | 24 h | ~30 min |
| Blob artifacts | Immutable, GRS optional; artifacts are content-addressed so corruption is detectable | 0 | minutes |
| Full export | Weekly `atlas export --full` → blob + **local Apple ecosystem copy** | 7 days | manual |
| Restore drill | **Quarterly, mandatory**, results recorded as a postmortem-style note | — | — |

The weekly export to a user-controlled location is the true disaster-recovery position: it survives
account loss, subscription cancellation, and vendor exit. The quarterly drill is what turns that
from a claim into a measurement.

---

## 7. Privacy, encryption, LGPD

| Concern | Approach |
|---|---|
| At rest | Azure platform encryption; blob and Postgres both |
| In transit | TLS 1.3 minimum |
| Secrets | Azure Key Vault; managed identity; **no secret in config, env, or repo** |
| Field-level | Account numbers, CPF, institution identifiers encrypted with a tenant-scoped key |
| **LGPD erasure** | **Cryptographic erasure** — destroy the tenant key. Satisfies deletion without violating the append-only ledger (BR-A04) |
| Telemetry | Aggregate and non-identifying only. No monetary values, no account identifiers leave the boundary (BR-A05) |
| LLM prompts | Minimised `FactSet` only; no raw ledger detail (BR-906) |

**On the append-only / right-to-erasure tension.** These genuinely conflict, and pretending
otherwise is how compliance debt is created. Cryptographic erasure resolves it: ciphertext without
a key is not personal data, the ledger's structural integrity is preserved, and the guarantee is
technical rather than procedural. Legal analysis in
[Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md).

---

## 8. Data quality as a measured property

Data quality is not a background concern here — an unreconciled ledger silently corrupts every
number above it. It is therefore measured as first-class SLIs
([Reliability Model](../04-engines/08-financial-reliability-model.md)):

| SLI | Definition | SLO |
|---|---|---|
| Coverage | % of net worth held in reconciled sources | ≥ 95% |
| Freshness | Hours since each source's last successful reconciliation | ≤ 72 h |
| Categorisation | % of expense value with a confirmed classification | ≥ 90% |
| Reconciliation drift | Absolute gap between source-reported and ledger-derived balance | ≤ R$ 1,00 |
| Mark staleness | Age of the oldest mark used in the current valuation | ≤ 48 h |

A forecast computed on degraded data is **still computed**, and **labelled `Degraded`** — never
silently produced as if clean (Law 8, "degrade, never lie").

---

**See also:** [Domain Model](../02-domain/04-domain-model.md) · [Ingestion & Integration](05-ingestion-and-integration.md) · [Security Strategy](06-security-strategy.md)
