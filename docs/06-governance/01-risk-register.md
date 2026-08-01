# Risk Register

**Status:** Living · **Owner:** CTO · **Reviewed:** quarterly

Ranked by **exposure** (probability × impact). Every risk has a named trigger — the observable
condition that means it is materialising — because a risk without a trigger is a worry, not a
managed item.

Scales: probability **L/M/H** · impact **1–5** · exposure = the product, banded.

---

## 🔴 Critical exposure

### RISK-001 — The project is built forever and never used
**P:** H · **I:** 5 · **Exposure:** CRITICAL

The single most likely way Atlas fails. An engineer building a tool for themselves optimises for the
building. The specification is ambitious enough to be an infinite project, and the intellectually
interesting parts (forecast, attribution, calibration) sit behind the boring ones (ledger, ingestion).

**Trigger:** M0–M2 not delivering a system in daily use; more time spent on M3+ design than on M0–M2
implementation.

**Mitigation**
- M0–M2 deliver a **usable** system before any interesting work begins — this sequencing is
  deliberate and is the primary control.
- Every milestone gate is about verified behaviour, not completed features.
- The specification is complete *now*, so design work cannot be used as productive-feeling avoidance
  of implementation.
- Manual-first ingestion (ADR-0010) means value arrives without waiting on anything external.

**Owner:** CTO. Reviewed at every milestone gate.

---

### RISK-002 — Tax engine is wrong
**P:** M · **I:** 5 · **Exposure:** CRITICAL

Brazilian tax is genuinely complex, and errors are silent: a wrong FI date looks exactly like a right
one. Because tax drag dominates `t_FI`, an error here invalidates the North Star and, worse, the
calibration record built on it.

**Trigger:** any golden-file discrepancy; any `TaxAmbiguity` rate above ~2% of computations; any
divergence from a professionally-prepared return.

**Mitigation**
- Independently verified golden corpora — never generated from the implementation.
- Every rule cites primary legislation; unciteable rules cannot be activated (BR-407).
- Conservative resolution of ambiguity (BR-408) — errors bias toward a later FI date.
- **Annual review by a qualified Brazilian tax professional** (§9 of the tax engine spec).
- Full rule tracing so any figure can be audited by a non-programmer.

---

### RISK-003 — The product becomes a noise machine
**P:** M · **I:** 5 · **Exposure:** CRITICAL

If the Signal Gate is tuned loosely, or is bypassed under pressure to "show more", Atlas becomes the
anxiety-amplification product it was built to replace. Trust would be lost quickly and permanently.

**Trigger:** notifications > 4/month at steady state (NFR-808); suppress:emit ratio trending toward
1:1; any request to "show daily changes".

**Mitigation**
- Structural: the Signal Gate's input type cannot represent a stochastic-only delta (BR-210).
- M4 exit gate requires ≥ 90% suppression over a 90-day backtest.
- Gate decisions are a monitored product metric, reviewed quarterly.
- NFR-808 treats excess notification as a **defect**, not a tuning preference.

---

## 🟠 High exposure

### RISK-004 — Determinism silently breaks
**P:** M · **I:** 4 · **Exposure:** HIGH

A canonicalisation change, an unordered parallel reduction, or a floating-point leak breaks
bit-identical replay. The damage is invisible for months and then invalidates every historical
comparison and the entire calibration record.

**Trigger:** determinism gate failure; `atlas_forecast_determinism_failures_total` > 0.

**Mitigation:** CI determinism gate on every PR · daily sampled production canary · cross-platform
canonicalisation golden tests · integer money · counter-based RNG per path · SEV-1 severity with
release blocking.

---

### RISK-005 — Specification and implementation diverge
**P:** H · **I:** 3 · **Exposure:** HIGH

Documentation drifts, becomes untrusted, then becomes actively misleading — which is worse than
having none, because decisions get made against it.

**Trigger:** rule-coverage gate failing; a document unchanged while its module changes materially.

**Mitigation:** docs in-repo, changed in the same PR (ADR-0019) · rule-coverage CI gate ·
stable identifiers cited in tests and commits · milestone documentation audit.

---

### RISK-006 — Data coverage never reaches usefulness
**P:** M · **I:** 4 · **Exposure:** HIGH

Manual entry is sustainable for months, not years. If coverage stalls below ~80%, every number above
the ledger is degraded, and the system quietly becomes decorative.

**Trigger:** Coverage SLI < 90% for two consecutive months; manual entry time > 30 min/month.

**Mitigation:** quick capture ≤ 10 s (FR-113) · CSV/OFX at M1 · coverage as a first-class SLI with
honest degradation labelling · aggregator evaluated at M7 on explicit cost/benefit ·
*nota de corretagem* parsing at M7.

**Note:** aggregator cost may exceed the entire infrastructure budget. The decision is deliberately
deferred and must be recorded either way.

---

### RISK-007 — Forecast model is systematically overconfident
**P:** M · **I:** 4 · **Exposure:** HIGH

Even with block bootstrap and regime switching, the model may understate tail risk — and the user
would plan on a probability that is too high, discovering the error only when it matters.

**Trigger:** PIT histograms U-shaped; calibration reliability outside tolerance at 1y.

**Mitigation:** the entire Calibration subsystem exists for this · ensemble spread reported as model
uncertainty · degraded reliability suppresses advice · drift opens a model review, never an
auto-retune.

**This is the risk the product is designed to detect rather than prevent** — which is the correct
posture, since it cannot be prevented in advance.

---

## 🟡 Medium exposure

### RISK-008 — Single maintainer unavailable
**P:** M · **I:** 3

Illness, life change, or loss of interest. There is no team.

**Trigger:** no commits for 90 days.

**Mitigation:** complete in-repo specification · ADRs recording all rationale · ≤ 30 min to a running
system · full data export means the *data* survives even if the *code* does not · open-source
readiness assessed at M8.

### RISK-009 — Scope grows without bound
**P:** H · **I:** 2

The specification is deliberately ambitious. Every module invites depth.

**Trigger:** milestone scope expanding after its start; XXL items entering development.

**Mitigation:** explicit non-goals (11 of them) · exit gates before progression · XXL is not a valid
size · six-card ceiling on Mission Control forcing removal before addition.

### RISK-010 — Azure cost drifts above the ceiling
**P:** L · **I:** 3

**Trigger:** `atlas_monthly_cost_usd` > US$30 for two consecutive months.

**Mitigation:** budget alerts at 50/80/100% · cost as an SLI with a SEV-3 incident · cost delta
required in every infra PR · scale-to-zero architecture.

### RISK-011 — Flutter or .NET ecosystem shift
**P:** L · **I:** 3

**Trigger:** LTS end-of-life announced without a migration path; framework strategy change.

**Mitigation:** domain has zero framework dependencies (MR-1) · no business logic in the client ·
data portability means a rewrite would not lose history.

### RISK-012 — Personal financial data breach
**P:** L · **I:** 5

Low probability, catastrophic impact. Aggregated financial data is more sensitive than any single
institution's view.

**Trigger:** anomalous auth failures; unexpected export operations; dependency CVE with active exploit.

**Mitigation:** passkeys only · field-level encryption with tenant-scoped keys · no financial data in
logs or telemetry · managed identity · minimal attack surface (no public signup, no payments) ·
security incident runbooks `RB-SEC-01..05`.

### RISK-013 — Legal exposure from advice framing
**P:** L · **I:** 4

**Trigger:** any copy-lint failure reaching production; any option naming a security.

**Mitigation:** ADR-0022 posture · copy lint in CI · non-empty `notModelled` enforced at runtime ·
no security-level options (BR-605) · disclaimers per the compliance document.

### RISK-014 — No staging environment
**P:** M · **I:** 2 — **Accepted**

A bad deploy reaches production directly.

**Mitigation:** twelve CI gates including determinism and contract tests · rollback < 5 min ·
expand/contract migrations only · single user means blast radius is bounded.

**Accepted deliberately** to hold the cost ceiling ([Infrastructure §5](../03-architecture/08-infrastructure.md)).

---

## 🟢 Low exposure — monitored

| ID | Risk | Trigger | Note |
|---|---|---|---|
| RISK-015 | Market data source changes format | Adapter golden test fails | Manual entry fallback always exists |
| RISK-016 | LLM provider changes or withdraws | API deprecation notice | Narrative is removable (BR-804); provider behind a port |
| RISK-017 | Simulation exceeds Job time budget | Runtime > 8 min | Fan out into parallel replicas — config only |
| RISK-018 | Postgres burstable throttling | Sustained CPU credit exhaustion | Scale up one tier; ~US$10 |
| RISK-019 | Calibration never reaches sufficient sample | n < 30 at 1y after 3 years | Report `Unknown` honestly; shorten claim horizons |
| RISK-020 | Kernel grows into a shared-utils dump | Any kernel addition without an ADR | ADR-gated; reviewed quarterly |

---

## Review process

Quarterly:
1. Re-score probability and impact — did anything move?
2. Check every trigger against actual telemetry.
3. Retire risks that have passed (e.g. RISK-001 after sustained daily use).
4. Add risks discovered through incidents and postmortems.
5. **Ask what is causing pain that is not on this list** — the most valuable question, and the one
   the register cannot ask itself.

---

**See also:** [Roadmap](../01-product/08-roadmap-and-milestones.md) · [Technical Debt Strategy](../05-engineering/05-technical-debt-strategy.md) · [Security Strategy](../03-architecture/06-security-strategy.md)
