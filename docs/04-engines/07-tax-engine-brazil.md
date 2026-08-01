# Tax Engine — Brazil

**Status:** Ratified · **Owner:** Domain / CTO · **Context:** C1 (Core)

> The hardest, deepest, and most defensible component in Atlas. In Brazil, tax drag and withdrawal
> sequencing move the FI date by **years**. Any FI engine without a real tax engine is wrong by
> construction.

> ⚠️ **Scope note.** This document specifies the engine's structure, the regimes it must model, and
> its correctness discipline. **Specific rates, thresholds, and statutory limits are held in
> versioned ruleset data, not here** ([ADR-0017](../03-architecture/adr/ADR-0017-versioned-tax-rulesets.md)),
> because they change. Every ruleset must be validated against primary legislation and reviewed by a
> qualified Brazilian tax professional before production use — see §9.

---

## 1. Design shape

```
ITaxJurisdiction  — pure, stateless, deterministic, no I/O, no clock
  ├─ ClassifyLot(instrument, acquisition, wrapper)        → TaxRegime
  ├─ ComputeDisposal(lots, disposal, date, yearContext)   → TaxConsequence
  ├─ ComputePeriodicWithholding(lots, date)               → TaxConsequence   // come-cotas
  ├─ ComputeIncomeTax(incomeEvents, yearContext)          → TaxConsequence
  ├─ ComputeDistribution(distribution, lot, yearContext)  → TaxConsequence
  ├─ ProjectTaxDrag(positions, policy, horizon)           → TaxDragCurve
  ├─ OptimiseWithdrawalSequence(positions, need, ctx)     → SequenceEvaluation[]
  └─ RulesetVersion                                       → SemVer + effective range
```

**Purity is the load-bearing property** (INV-052, BR-400). Time is a parameter, never read.
This is what makes a 2026 forecast replayable in 2034 under 2026 law — and therefore what makes
calibration meaningful at all.

---

## 2. Regimes to model

Each is a distinct rule family with its own basis, timing, rate structure, and exemptions.

| Regime | Instruments | Key characteristics |
|---|---|---|
| `RendaVariavel` | Ações, ETFs, BDRs | Self-assessed via DARF; monthly netting; day-trade segregated; small-sale exemption |
| `FII` | Fundos imobiliários | Distributions exempt under conditions; gains taxed with **no** small-sale exemption |
| `RendaFixa` | CDB, LC, Tesouro, debêntures | *Tabela regressiva* by holding period; withheld at source |
| `Isento` | LCI, LCA, CRI, CRA, poupança, debêntures incentivadas | Exempt for individuals |
| `FundoAberto` | Multimercado, renda fixa, ações funds | **Come-cotas** (May/Nov) for non-equity funds; equity funds taxed on redemption |
| `FundoFechado` | Closed-end funds | Post-2024 periodic taxation regime |
| `Previdencia` | PGBL / VGBL | Progressive vs regressive election; PGBL deductibility; VGBL taxes gains only |
| `Exterior` | Foreign assets, offshore | Distinct regime; annual taxation; FX gain treatment |
| `Cripto` | Crypto assets | Own reporting obligations and thresholds |
| `RealAsset` | Property | Capital gain with reduction factors and reinvestment exemptions |

---

## 3. The concepts that dominate the FI date

These are where an engine that "handles taxes" and an engine that is *correct* diverge.

### 3.1 Come-cotas
Semi-annual mandatory withholding on open-ended non-equity funds, taken **in quotas** rather than
cash. Its significance is not the rate — it is that it **forces realisation of gains that would
otherwise compound untaxed**. Over 20 years the drag is substantial, and it is the single strongest
argument for wrapper selection. Modelling it as an annual percentage haircut is wrong; it must be
modelled as a discrete event on the actual schedule, reducing quota count.

### 3.2 Tabela regressiva
The IR rate on fixed income declines with holding period. Consequences the engine must capture:
- **Rebalancing has a tax cost** that depends on *which lot* is sold and *how long it was held*.
- Early redemption can be materially more expensive than the nominal rate difference suggests.
- The optimal withdrawal sequence in decumulation depends on the age of each lot, not just its type.

### 3.3 The equity small-sale exemption
Monthly sales of *ações* below a statutory threshold are exempt from gains tax. This creates a
genuine, legal, and non-obvious decumulation strategy: **staged monthly disposals within the
exemption**. The engine must track cumulative monthly usage (BR-404) and the Advisory layer can
surface sequencing options that exploit it. Note it does **not** apply to FIIs — a distinction that
routinely trips up even sophisticated individual investors.

### 3.4 PGBL vs VGBL
- **PGBL** — contributions deductible up to a percentage of taxable income; taxed on the **full
  amount** at withdrawal.
- **VGBL** — no deduction; taxed on **gains only**.
- Both offer a progressive/regressive taxation election that is effectively irreversible in
  practice.

This is one of the highest-leverage decisions available to a Brazilian saver, and it is
**horizon-dependent** — the right answer changes with time to withdrawal, marginal rate now vs
later, and deduction headroom. Exactly the kind of question Atlas should evaluate as ranked options.

### 3.5 Withdrawal sequencing
In decumulation, the **order** of drawdown across wrappers and lots can shift the FI date by years.
It is a first-class `Policy` dimension (INV-090, BR-406) and one of the strongest option classes the
Advisory engine can offer — because it costs the user nothing to change and can be worth a great
deal.

---

## 4. The ruleset

```yaml
ruleset:
  version: 2026.1.0
  effectiveFrom: 2026-01-01
  effectiveTo: null
  legalBasis:
    - { rule: "rendaFixa.tabelaRegressiva", source: "<statute reference>", verifiedOn: 2026-08-01 }
    - { rule: "rendaVariavel.isencaoMensal",  source: "<statute reference>", verifiedOn: 2026-08-01 }
  regimes:
    rendaFixa:
      brackets: [ { maxDays: …, rate: … }, … ]
      withholding: atSource
    rendaVariavel:
      standardRate: …
      dayTradeRate: …
      monthlyExemptionThreshold: …          # ações only
      exemptionAppliesTo: [ Acao ]
      lossCarryForward: { scope: …, expiry: … }
    fii:
      distributionExemption: { conditions: [ … ] }
      gainRate: …
      monthlyExemption: false
    fundoAberto:
      comeCotas: { months: [5, 11], rateShortTerm: …, rateLongTerm: … }
      equityFundExempt: true
    previdencia:
      pgbl: { deductibilityLimit: … }
      regressiveTable: [ { minYears: …, rate: … }, … ]
      progressiveTable: reference: irpf.progressive
```

Every rule carries a `legalBasis` with a `verifiedOn` date. **A rule with no cited legal basis
cannot be activated** (BR-407). This is what makes external review by a tax professional tractable
— they read the ruleset, not the code.

---

## 5. Rule tracing

Every `TaxConsequence` carries a `RuleTrace` (INV-050):

```
TaxConsequence
  taxableBase: R$ 12.400,00
  taxDue:      R$ 1.860,00
  rulesetVersion: 2026.1.0
  ruleTrace:
    1. classify(lot) → RendaVariavel        [rule: classification.equity]
    2. exemptionCheck(monthly sales)  → not applicable, threshold exceeded
                                             [rule: rendaVariavel.isencaoMensal]
    3. netAgainstLosses(carryForward) → R$ 12.400,00
                                             [rule: rendaVariavel.lossCarryForward]
    4. applyRate(standard)            → R$ 1.860,00
                                             [rule: rendaVariavel.standardRate]
    5. deductWithheld(dedo-duro)      → R$ 1.859,38 payable
                                             [rule: rendaVariavel.retencaoFonte]
```

**A tax figure that cannot explain itself is rejected.** The trace is what makes the engine
auditable, debuggable, and reviewable by a non-programmer — and it is what the user sees when they
ask "why this number?".

---

## 6. Corporate actions and cost basis (hotspot H1)

| Action | Basis treatment |
|---|---|
| Split / reverse split | Quantity adjusts; **total basis preserved**; unit cost recalculated |
| Bonificação (bonus shares) | Basis increases by the amount declared by the company |
| Subscription rights | Exercised → basis increases by cost; sold → disposal event |
| Merger / incorporation | Basis carries to the successor; cash portion is a disposal |
| Spin-off | Basis apportioned per company-declared ratio |
| Amortisation (FII) | **Reduces basis**, not income — a frequent and costly error |
| JCP | Taxed at source as income, distinct from dividends |

**Brazilian equities use *custo médio*** across all lots of a commodity (INV-043). Lots remain
individually tracked for audit and for regimes requiring lot identification (e.g. `Exterior`), but
the *taxable* basis for equities is the weighted average. Modelling per-lot FIFO — the instinctive
choice for a developer used to US rules — produces wrong Brazilian tax.

---

## 7. Correctness discipline

This module has the strictest testing regime in the system:

| Technique | Application |
|---|---|
| **Golden files** | Real, redacted scenarios with independently verified expected outputs, per ruleset version |
| **Property tests** | Basis conservation across corporate actions; monotonicity of the regressive table; exemption consumption never exceeds the limit |
| **Differential tests** | Cross-check against independent manual computation for a curated scenario set |
| **Ruleset validation** | Schema + legal-basis presence + effective-date continuity (no gaps, no overlaps) |
| **Historical replay** | Every historical ruleset version replays its golden corpus on every build |
| **Ambiguity flagging** | Genuinely ambiguous cases compute the **conservative (higher-tax)** branch and raise `TaxAmbiguity` (BR-408) |

### The conservatism rule
When the correct treatment is genuinely uncertain, the engine takes the branch that produces the
**higher tax and therefore the later FI date**. Rationale: the cost of optimistic error is a plan
built on a projection that cannot be achieved; the cost of conservative error is a pleasant
surprise. In an asymmetric-loss setting, this is the only defensible default — and the ambiguity is
flagged for review rather than buried.

---

## 8. Interaction with other engines

| Consumer | Use |
|---|---|
| **Forecast** | Every simulated period routes through the engine — 50,000 paths × 660 months. **Performance-critical** |
| **Advisory** | Every option's cost is after-tax (BR-603, INV-154) |
| **Positions** | Basis mechanics; tax treatment classification |
| **Attribution** | Tax law change is a `Structural` driver |

### Performance
~33 million tax computations per forecast run. Requirements: no allocation in the hot path,
precomputed rate lookup tables per ruleset version, memoised classification per lot, and a fast path
for the overwhelmingly common cases (no disposal, no come-cotas month). The pure-function design is
what makes aggressive caching safe.

---

## 9. Governance and the limits of this specification

**Atlas is not a tax authority, and this specification is not tax advice.**

| Control | Practice |
|---|---|
| Ruleset review | **Annual, plus on any legislative change**, by a qualified Brazilian tax professional |
| Legal basis | Every rule cites primary legislation with a verification date |
| Ambiguity | Flagged to the user, never silently resolved |
| User-facing framing | Tax figures are **estimates for planning**, explicitly not a filing position |
| Filing | Atlas never files, never submits, never generates a binding declaration. It may produce a DARF *worksheet* for the user to verify |
| Disclaimer | Present wherever tax figures are shown, per [Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md) |

This is the one module where the specification explicitly defers to outside expertise. Recorded as
`RISK-002` in the [Risk Register](../06-governance/01-risk-register.md).

---

**See also:** [ADR-0014](../03-architecture/adr/ADR-0014-brazil-only-tax-engine.md) · [ADR-0017](../03-architecture/adr/ADR-0017-versioned-tax-rulesets.md) · [Business Rules BR-4xx](../02-domain/05-business-rules.md)
