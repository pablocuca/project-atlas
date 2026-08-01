# Compliance & Legal Posture

**Status:** Ratified · **Owner:** CTO

> ⚠️ **This document is an engineering posture, not legal advice.** It records the design decisions
> taken to stay well inside regulatory boundaries and the assumptions behind them. Before any use
> beyond a single private individual — and certainly before open-sourcing or serving a second user —
> it must be reviewed by a qualified Brazilian lawyer.

---

## 1. What Atlas is, legally

**A personal record-keeping and analysis tool, operated by an individual for their own use.**

Three properties keep it firmly outside the regulated perimeter, and they are architectural rather
than promised:

| Property | Consequence |
|---|---|
| **Holds no funds, initiates no payments** (NG-03) | Not a payment institution under BACEN; no licensing, capital, or audit obligations |
| **Recommends no specific securities** (NG-04) | Not *consultoria de valores mobiliários* under CVM rules |
| **Single private user, not offered as a service** (ADR-0011) | No consumer-relationship or service-provider obligations |

Each is enforced in code, not merely in policy: there is no payment integration, options operate at
asset-class level only (BR-605), and there is no signup path.

---

## 2. The investment-advice boundary

In Brazil, providing individualised securities recommendations professionally is a regulated
activity requiring CVM registration.

### Where Atlas sits, and why it is defensible

| Regulated advice | What Atlas does |
|---|---|
| "Buy PETR4" | ❌ Never — no option may name a security (BR-605) |
| "Allocate 60% to equities" | ❌ Not as an instruction |
| "Option 2: increasing equity allocation to 60% changes your median FI date by −7 months and increases your `JobLossInCrash` ruin probability by 0.4pp. Not modelled: your subjective liquidity preference, employer plan fees." | ✅ Evaluated option with quantified tradeoffs and disclosed unknowns |

Four mechanisms hold that line, and all four are enforced mechanically:

1. **Minimum two options** (BR-601). A single "option" is a prescription wearing a costume.
2. **Copy lint** in CI blocks imperative constructions ("you should", "buy", "sell", "act now").
3. **Non-empty `notModelled`** enforced at runtime (INV-151). The system always states what it does
   not know.
4. **No security-level granularity.** Options operate on asset classes, wrappers, and rates.

### The defensible position
> Atlas evaluates the consequences of choices the user might make, using their own data and stated
> assumptions, and discloses what it cannot model. The decision, and the responsibility for it,
> remain entirely with the user.

**Reassessment triggers:** offering Atlas to any other person; adding security-level options; adding
execution capability. Any of these requires legal review before proceeding.

---

## 3. Tax

**Atlas produces planning estimates, never filing positions.**

| Boundary | Practice |
|---|---|
| Never files | No integration with Receita Federal. Ever |
| Never generates a binding declaration | A DARF **worksheet** for user verification is the maximum (FR-513) |
| Always traceable | Every figure carries a `RuleTrace` and ruleset version |
| Always cites law | Rules without a cited legal basis cannot be activated (BR-407) |
| Conservative on ambiguity | Higher-tax branch computed, ambiguity flagged (BR-408) |
| Professionally reviewed | Annual review by a qualified Brazilian tax professional |

Required disclaimer wherever tax figures appear:

> *Estimate for planning purposes, computed under ruleset {version}. Not a filing position. Verify
> with a qualified professional before acting.*

`RISK-002` tracks this as a critical risk. Tax error is silent, and silence is what makes it
dangerous.

---

## 4. LGPD

Atlas processes the personal data of exactly one person — its operator — who is simultaneously data
subject and controller. Most LGPD machinery is therefore inapplicable today, but the **technical
capabilities** are built now because retrofitting them is expensive.

| Right / obligation | How Atlas satisfies it |
|---|---|
| **Access** (Art. 18 II) | Full export in open formats, one command (FR-930) |
| **Portability** (Art. 18 V) | Same export, self-describing, vendor-neutral |
| **Correction** (Art. 18 III) | Bitemporal correction preserving audit history |
| **Deletion** (Art. 18 VI) | **Cryptographic erasure** — destroy the tenant key (ADR-0020) |
| **Information about processing** | This specification library |
| **Security** (Art. 46) | [Security Strategy](../03-architecture/06-security-strategy.md) |
| **Breach notification** (Art. 48) | Assessment within 24 h, documented regardless of outcome |
| Data residency | Brazil South — not mandated, but removes a class of argument |

### The append-only vs erasure conflict
ADR-0002 requires an immutable ledger; LGPD grants a deletion right. These genuinely conflict, and
pretending otherwise is how compliance debt is created.

**Resolution (ADR-0020):** personal identifiers are encrypted with a tenant-scoped key. Erasure
destroys the key. Ciphertext without a key is not, on the prevailing reading, personal data.
Structural ledger integrity survives; the guarantee is technical and instantaneous rather than a
promise that a deletion job reached every backup and replica.

**Assumption requiring legal validation:** that cryptographic erasure satisfies Art. 18 VI in
Brazilian practice. Documented as an assumption, not a conclusion.

---

## 5. Open Finance Brasil

Atlas is **not** an Open Finance participant and does not seek to be — participation requires
authorised-institution status.

| Approach | Posture |
|---|---|
| Direct participation | ❌ Not pursued. Regulatory burden is prohibitive |
| Commercial aggregator | ⚠️ Evaluated at M7 on cost/benefit; the aggregator holds the regulated relationship |
| Screen scraping | ❌ **Prohibited.** Likely breaches terms of service; unacceptable credential handling |
| Manual and file-based import | ✅ Primary approach (ADR-0010) |

If an aggregator is adopted, Atlas stores only scoped, revocable read tokens — never bank
credentials. Credential handling remains a prohibited operation regardless of convenience.

---

## 6. Third-party data

| Source | Basis | Constraint |
|---|---|---|
| B3 quotes | Public data | Respect terms; no redistribution |
| BCB SGS (IPCA, SELIC, CDI) | Public API | No restriction |
| Tesouro Direto files | Public data | No redistribution |
| CVM fund data | Public data | No redistribution |
| LLM provider | Commercial terms | Zero data retention where offered; minimal payload |

**Atlas never redistributes third-party market data.** It consumes it for one user's own analysis.
This matters if the project is ever open-sourced: the *code* may be shared; the *data* may not.

---

## 7. If Atlas is open-sourced

Every posture above assumes a single private user. Opening the project changes the analysis
materially, and the following must be resolved **before** publication:

| Question | Consideration |
|---|---|
| Licence | Permissive (MIT/Apache-2.0) with an explicit no-warranty and no-advice clause |
| Advice framing | Self-hosting users receive the same option-based output; disclaimers must survive forking |
| Tax rulesets | Distributed as data with legal-basis citations and an explicit accuracy disclaimer |
| Liability | Standard OSS disclaimers; **no support obligation**; no hosted offering |
| Hosted service | ❌ **Not offered.** Hosting would create a service relationship and a regulatory perimeter |
| Data | No fixtures, no market data, no personal data — synthetic only |
| Trademark | Naming reviewed before publication |

**The decisive line:** distributing *software* is categorically different from operating a *service*.
Atlas may be open-sourced; it will not be hosted for others.

---

## 8. Standing disclaimers

Present in the application, in the repository, and in any published material:

> **Atlas is a personal analysis tool. It is not financial, investment, tax, or legal advice.**
> All figures are estimates produced from user-supplied data under stated assumptions, and every
> projection carries uncertainty that may be larger than shown. Financial decisions are the user's
> own. Consult qualified professionals.

> **Probabilities are model outputs, not guarantees.** Atlas reports its own historical calibration
> where sufficient data exists, and reports `Unknown` where it does not. Long-horizon projections
> are extrapolations and are labelled as such.

---

## 9. Review schedule

| Item | Cadence |
|---|---|
| Tax ruleset legal review | Annual + on legislative change |
| Advice-boundary posture | Annual, and before any scope change |
| LGPD posture | Annual, and before any second user |
| Third-party terms | Annual |
| This document | Annual, and before open-sourcing |

---

**See also:** [ADR-0022](../03-architecture/adr/ADR-0022-advice-posture.md) · [ADR-0020](../03-architecture/adr/ADR-0020-cryptographic-erasure.md) · [Non-Goals](../00-foundation/04-non-goals.md) · [Risk Register](01-risk-register.md)
