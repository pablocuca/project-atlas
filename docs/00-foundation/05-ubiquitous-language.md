# Ubiquitous Language

**Status:** Ratified · **Owner:** Domain / CTO · **Last reviewed:** 2026-08-01

This glossary is **binding**. A term defined here means exactly this in code, database columns,
API contracts, UI copy, tests, and documents. Introducing a synonym is a defect. Changing a
definition requires an ADR and a coordinated rename.

> **Convention.** Terms are written in `PascalCase` when they name a type in the model, and in
> plain English when they name a concept. Portuguese terms are retained *untranslated* where the
> Brazilian regulatory meaning has no English equivalent — translating them loses precision.

> **Language.** This glossary is **English-canonical**. Code, identifiers, and this specification are
> English-only; the *product surface* is bilingual (pt-BR default, en available) per
> [ADR-0023](../03-architecture/adr/ADR-0023-bilingual-product-english-spec.md). See §I for the
> binding translation policy — it constrains what may and may not be translated in the UI.

---

## A. Core mission vocabulary

| Term | Definition | Not to be confused with |
|---|---|---|
| **Financial Independence (FI)** | The state defined in [Mission §3](02-mission-and-north-star.md): after-tax, real, non-labour income sustains the Spending Floor through the Horizon with probability ≥ Confidence Target | Retirement, having "enough", 25× expenses |
| **FI Date** | The random variable `t_FI`. Always reported as a distribution (P10/P50/P90) | A single date |
| **Target Date** | The user's *declared* desired FI date. An input, not an output | FI Date |
| **Confidence Target (`c`)** | Required success probability for FI to be declared reached. Default 85% | Confidence interval |
| **Horizon (`H`)** | Age through which the plan must hold. Default 95 | Life expectancy |
| **Spending Floor** | Essential + Committed Discretionary spending, in real terms, non-stationary | Budget, average spend |
| **Policy (`π`)** | A complete, coherent set of user-controllable decisions (savings rate, allocation, debt order, housing, career, wrapper choice) | Strategy, plan |
| **Status Quo Policy (`π₀`)** | The policy currently in force, inferred from behaviour and confirmed by the user | Target policy |
| **Policy Space (`Π`)** | The enumerated set of feasible alternative policies the Recommendation Engine evaluates | All possible actions |
| **Freedom Ratio** | Realised after-tax non-labour income ÷ Essential spending, trailing 12 months | Passive income, dividend yield |

---

## B. Ledger and money

| Term | Definition |
|---|---|
| **Ledger** | The append-only, bitemporal, double-entry system of record. The only source of truth |
| **Account** | A named node in the chart of accounts. Typed: `Asset`, `Liability`, `Equity`, `Income`, `Expense` |
| **JournalEntry** | An atomic, balanced set of Postings representing one economic event. Immutable |
| **Posting** | A single debit or credit against one Account, in one Commodity. Never exists alone |
| **Commodity** | Any unit of value: `BRL`, `USD`, a ticker (`PETR4`), a fund quota, a bond. Currency is a Commodity |
| **Money** | `(amount: integer minor units, commodity: Commodity)`. **Never a floating-point number** |
| **ValidTime** | When the fact was true in the real world (e.g. trade date) |
| **DecisionTime** | When Atlas learned the fact (e.g. when the broker file was imported) |
| **Bitemporal** | Every ledger fact carries both ValidTime and DecisionTime intervals |
| **Correction** | A new JournalEntry that reverses and replaces a prior one. Never a mutation |
| **Reconciliation** | Proving that a Source's reported balance equals the Ledger's derived balance at a point in time |
| **Source** | An external origin of financial facts: a broker, a bank, a CSV, manual entry |

---

## C. Positions, valuation, and taxation

| Term | Definition |
|---|---|
| **Lot** | A specific acquisition of a Commodity: quantity, unit cost, acquisition date, tax treatment |
| **Position** | Aggregate holding of one Commodity across all Lots, at a point in time |
| **CostBasis** | Acquisition cost of a Lot or Position, per Brazilian rules (*custo médio* for equities) |
| **Mark** | An external price observation for a Commodity at a point in time |
| **Valuation** | A projection: Positions × Marks → value, at a stated instant, in a stated Commodity |
| **TaxLot** | A Lot annotated with its Brazilian tax regime (see below). The unit of tax computation |
| **TaxRegime** | The rule set governing a TaxLot: `RendaVariavel`, `RendaFixa`, `FII`, `FundoAberto`, `Previdencia`, `Exterior`, `Isento` |
| **Come-cotas** | The semi-annual (May/Nov) mandatory withholding on open-ended Brazilian funds. Untranslated |
| **Tabela Regressiva** | The regressive IR schedule on fixed income (22.5% → 15% by holding period). Untranslated |
| **DARF** | The Brazilian tax payment slip generated for self-assessed capital gains. Untranslated |
| **Isenção** | A statutory exemption (e.g. FII distributions, ≤R$20k/month equity sales). Untranslated |
| **TaxDrag** | Realised cumulative tax cost expressed as annualised return reduction |
| **WithdrawalSequence** | The order in which accounts/lots are drawn down in decumulation. A first-class Policy dimension |

---

## D. The Digital Twin and simulation

| Term | Definition |
|---|---|
| **Digital Twin** | The complete, consistent, versioned model of the user's financial life at an instant |
| **TwinSnapshot** | An immutable, content-addressed capture of the Twin. Input to every Forecast |
| **TwinState** | The mutable current projection, continuously rebuilt from the Ledger and external facts |
| **Forecast** | An immutable artifact: `(TwinSnapshot hash, ModelVersion, Parameters, Seed) → outcome distribution` |
| **ForecastArtifact** | The stored, replayable form of a Forecast. Never deleted, never mutated |
| **ModelVersion** | Semantic version of the stochastic model. Changing it invalidates comparisons, not history |
| **Path** | One simulated trajectory of the Twin through time |
| **Scenario** | A named, parameterised deviation from base assumptions (e.g. `JobLoss18Months`, `IPCA+2pp`) |
| **Counterfactual** | A Forecast run on the same TwinSnapshot under a different Policy |
| **HumanCapital** | Risk-adjusted present value of expected future labour income. Usually the largest asset |
| **SequenceRisk** | Risk that the *order* of returns, not their average, causes ruin |

---

## E. Change, signal, and attribution

| Term | Definition |
|---|---|
| **Delta** | A measured change in a metric between two instants |
| **Attribution** | Decomposition of a Delta into contributing Drivers with quantified shares |
| **Driver** | A named variable to which part of a Delta is attributed (e.g. `SavingsRate`, `SELIC`, `EquityMark`) |
| **DriverClass** | `Controllable` \| `Structural` \| `Stochastic`. Determines whether a Delta may alert |
| **Controllable** | Caused by a user decision — spending, saving, income action, debt action, allocation change |
| **Structural** | Caused by an exogenous but persistent shift — rates, inflation, tax law, mortgage reset |
| **Stochastic** | Caused by market marks. **Never alerts.** Consumes variance budget |
| **Signal Gate** | The rule set a Delta must pass to become user-visible as an event |
| **ChangeEvent** | A Delta that passed the Signal Gate, with its Attribution attached |
| **Noise** | A Delta that failed the Signal Gate. Retained, inspectable, never surfaced by default |

---

## F. Reliability model (SRE vocabulary, financially bound)

| Term | Definition |
|---|---|
| **FinancialSLI** | A measurable indicator of financial system health (e.g. Savings Rate, Emergency Coverage) |
| **FinancialSLO** | The target an SLI must hold (e.g. Savings Rate ≥ 30% trailing 3 months) |
| **ErrorBudget** | Permitted cumulative deviation from an SLO over a window, expressed in its natural unit |
| **BurnRate** | Speed at which an ErrorBudget is being consumed, relative to the window |
| **FinancialIncident** | A sustained SLO breach that has been declared, with a timeline and an owner |
| **Runbook** | A documented, repeatable procedure for responding to a class of Incident |
| **Postmortem** | Blameless written analysis of a resolved Incident, with corrective actions |
| **HealthScore** | Composite 0–100 score across reliability dimensions. Noise-immune by construction |
| **ForecastReliability** | Calibration quality of Atlas's own past forecasts. An SLI about the system itself |

---

## G. Progression (gamification)

| Term | Definition |
|---|---|
| **DisciplineIndex** | Adherence of realised behaviour to the declared Policy. Process-only |
| **ContributionStreak** | Consecutive periods meeting the declared contribution commitment |
| **SavingsVelocity** | Rate of change of savings capacity, deflated |
| **FreedomVelocity** | FI-days gained per calendar month, **Controllable drivers only** |
| **OperationalXP** | Points awarded for completed process actions. Never for outcomes or returns |
| **ArchitectureLevel** | Tier reflecting the structural maturity of the user's financial setup |

---

## H. Terms explicitly banned

Using these in code, UI, or documents is a review-blocking defect.

| Banned | Reason | Use instead |
|---|---|---|
| "Balance" (unqualified) | Ambiguous between ledger balance and available cash | `LedgerBalance`, `AvailableCash` |
| "Profit" / "Lucro" | Ambiguous between realised, unrealised, gross, after-tax | `RealisedGain`, `UnrealisedGain`, `AfterTaxGain` |
| "Return" as a headline | Violates Law 2 | `AttributedDelta` |
| "Retirement" | Not what Atlas models | `FinancialIndependence` |
| "Guaranteed", "safe", "risk-free" | False under uncertainty; also a legal hazard | State the probability |
| "Recommendation" as an instruction | Violates Law 14 | `RankedOption` |
| "Score" without a defined scale | Meaningless | Name the specific index |
| "Net worth" as a health metric | An outcome, not a controllable | `HealthScore`, `FreedomRatio` |

---

## I. Translation policy (binding on the UI)

The product surface is bilingual; this glossary is not. Three tiers govern what happens to a term
when it reaches the user. Full detail: [Localisation Strategy](../01-product/11-localisation-strategy.md).

### Tier 1 — Never translated, in any locale

`come-cotas` · `tabela regressiva` · `isenção` · `DARF` · `IRRF` · `ganho de capital` ·
`custo médio` · `PGBL` · `VGBL` · `FGTS` · `JCP` · `nota de corretagem` · `saldo devedor` ·
`SAC` · `Price` · `CDI` · `SELIC` · `IPCA` · `IGP-M`

These appear **verbatim in the English UI**, with an English gloss on first use. A regulatory term
points at a legal referent; a translation points at a description that resembles it and drifts when
the law changes. Translating one is the same category of error as translating *habeas corpus*.

### Tier 2 — Translated against a fixed canonical pair

| English (canonical) | pt-BR |
|---|---|
| Financial Independence (FI) | Independência Financeira (IF) — **FI-days → dias-IF** |
| FI Date · Spending Floor · Confidence Target | Data de IF · Piso de Gastos · Meta de Confiança |
| Freedom Ratio · Health Score · Discipline Index | Índice de Liberdade · Índice de Saúde · Índice de Disciplina |
| Controllable / Structural / Stochastic | Controlável / Estrutural / Estocástico |
| Change Event · Digital Twin | Evento de Mudança · Gêmeo Digital |
| **Ranked Option** | **Opção Avaliada** — never *"Recomendação"* (§H) |

The pairing is fixed and versioned. Introducing a second Portuguese rendering of a term is the same
defect as introducing an English synonym — see §H.

### Tier 3 — English acronyms inside Portuguese prose

`SLI` · `SLO` · `error budget` · `burn rate` · `runbook` · `postmortem` · `SEV-1..4` · `incident`

Operational vocabulary stays English because that is how the audience speaks; financial vocabulary is
translated. *"Discretionary Error Budget"* is Tier 2 (Orçamento de Erro Discricionário) precisely
because it is a financial concept, not an operational one.

### Terms banned in Portuguese

Extends §H, and is **compliance-critical** — the advice boundary must hold in both languages:

| Banned (pt-BR) | Reason | Use instead |
|---|---|---|
| "Recomendação", "recomendamos", "indicamos" | Carries regulatory connotation the English "option" does not | "Opção Avaliada" |
| "Você deve", "você deveria", "compre", "venda", "invista em" | Imperative advice — violates Law 14 | Evaluated-option phrasing |
| "Garantido", "seguro", "sem risco" | False under uncertainty; legal hazard | State the probability |
| "Aja agora", "não perca", "última chance" | Urgency framing — violates Law 10 | — |
| "Lucro" | Ambiguous between realised/unrealised/pre-tax | "Ganho realizado", "ganho não realizado" |
| "Aposentadoria" | Not what Atlas models | "Independência Financeira" |

Each of these is enforced by the per-locale copy lint (BR-B04), not by review.

---

**See also:** [Localisation Strategy](../01-product/11-localisation-strategy.md) · [Domain Model](../02-domain/04-domain-model.md) · [Bounded Contexts](../02-domain/02-bounded-contexts.md)
