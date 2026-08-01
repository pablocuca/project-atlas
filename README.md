# Project Atlas

**Financial Independence Operating System**

> A Digital Twin of a financial life, under continuous simulation, with an SRE-grade reliability
> model wrapped around it — built to answer one question, honestly, for twenty years:
>
> **"Given everything I know today, and everything I cannot know, am I still on the best available
> path to financial freedom — and if not, what changed, how much did it cost, and what are my
> options?"**

---

## Status

**M0 in progress.** The specification was written first, deliberately
([ADR-0019](docs/03-architecture/adr/ADR-0019-docs-precede-code.md)); implementation began with the
part of M0 that is highest-risk and hardest to reverse — `Atlas.Kernel` (`Money`, `Commodity`,
bitemporal time types) and the pure `Ledger.Domain` model (accounts, journal entries, bitemporal
replay), fully property-tested, with no persistence yet. See
[`docs/01-product/08-roadmap-and-milestones.md`](docs/01-product/08-roadmap-and-milestones.md) for
the M0 exit gate this is working toward, and [`docs/decisions/`](docs/decisions/) for implementation
decisions made along the way.

📖 **Start with [`docs/README.md`](docs/README.md)** — the specification library index.

```
dotnet test atlas.sln   # 29 tests: xUnit facts + FsCheck properties + module-boundary checks
```

---

## What Atlas is not

It is not a portfolio tracker, a budgeting app, a bank, or an investment adviser. It refuses eleven
things explicitly, and each refusal has a written reason: [Non-Goals](docs/00-foundation/04-non-goals.md).

The most important refusal: **market movement never generates an alert.** At a twenty-year horizon,
daily volatility swamps a month of saving by an order of magnitude. A system that reports that as an
event is an anxiety machine. Atlas attributes every change to *Controllable*, *Structural*, or
*Stochastic* causes, and only the first two are ever allowed to speak
([ADR-0009](docs/03-architecture/adr/ADR-0009-attribution-gated-alerting.md)).

---

## The four things that make it different

| | |
|---|---|
| **Attribution before display** | No number is shown as a change until its cause is decomposed. The default state of the product is silence |
| **It grades itself** | Every forecast is stored immutably and scored against reality. When calibration degrades, Atlas suppresses its own advice and says why |
| **Tax is core domain** | In Brazil, tax drag and withdrawal sequencing move the FI date by *years*. The tax engine is the deepest component in the system |
| **The data outlives the platform** | One command exports everything to open formats. Azure, .NET, and Flutter are replaceable; the ledger is not |

---

## Architecture at a glance

```
① TRUTH LOOP        ingest → bitemporal double-entry ledger → reconcile
② FORESIGHT LOOP    twin snapshot → ensemble simulation → immutable artifact
③ DECISION LOOP     attribute → signal gate → SLOs → ranked options
④ CALIBRATION LOOP  observe → score → gate the advice
```

**Stack:** .NET 10 modular monolith · PostgreSQL (bitemporal) · Flutter (iOS + web) ·
Azure Container Apps, scale-to-zero · OpenTelemetry · Bicep · GitHub Actions
**Run-rate ceiling:** US$30/month, treated as an architectural constraint
([ADR-0015](docs/03-architecture/adr/ADR-0015-cost-ceiling.md))

---

## Locked decisions

| Decision | Choice |
|---|---|
| Jurisdiction | Brazil only, deep — behind an `ITaxJurisdiction` seam |
| Product boundary | Single-tenant core, open-source-ready seams |
| Cloud ceiling | ≤ US$30/month |
| Advice posture | Ranked options with quantified tradeoffs — never instructions |
| Language | Bilingual product (pt-BR default, en) · English-only specification and codebase |

---

## 🇧🇷 Sobre este projeto

**Atlas é um Sistema Operacional de Independência Financeira** — um gêmeo digital da sua vida
financeira, sob simulação contínua, com um modelo de confiabilidade no estilo SRE em volta dele.

Ele responde a uma pergunta: *"considerando tudo o que sei hoje, continuo no melhor caminho possível
para a independência financeira — e se não, o que mudou, quanto custou, e quais são minhas opções?"*

**Diferencial:** movimento de mercado **nunca** gera alerta. Em um horizonte de 20 anos, a
volatilidade diária supera um mês inteiro de aportes em uma ordem de grandeza. Atlas atribui toda
mudança a causas *Controláveis*, *Estruturais* ou *Estocásticas* — e só as duas primeiras podem
falar. O sistema também **avalia a própria calibração** e suspende os próprios conselhos quando ela
se degrada.

O **produto é bilíngue** (pt-BR padrão, inglês disponível). A **especificação e o código são apenas
em inglês** — fonte única de verdade, sem risco de divergência entre traduções
([ADR-0023](docs/03-architecture/adr/ADR-0023-bilingual-product-english-spec.md)). Termos
regulatórios brasileiros (*come-cotas*, *DARF*, *isenção*, *tabela regressiva*) **nunca são
traduzidos**, nem mesmo na interface em inglês — traduzir um termo legal destrói sua referência.

📖 Comece por [`docs/README.md`](docs/README.md) · Estratégia de idioma:
[Localisation Strategy](docs/01-product/11-localisation-strategy.md)

---

## Specification library

| Layer | Contents |
|---|---|
| [00 Foundation](docs/00-foundation/) | Vision · Mission & North Star · 14 Product Laws · Non-Goals · Ubiquitous Language |
| [01 Product](docs/01-product/) | Persona & JTBD · FRs · NFRs · Quality Attributes · Dashboard · Gamification · UX · Roadmap · Backlog · Stories |
| [02 Domain](docs/02-domain/) | Event Storming · 17 Bounded Contexts · Context Map · Domain Model · Business Rules |
| [03 Architecture](docs/03-architecture/) | Architecture Vision · C4 · Modular Monolith · Data · Ingestion · Security · Observability · Infrastructure · DevOps · **22 ADRs** |
| [04 Engines](docs/04-engines/) | Digital Twin · Forecast · Simulation · Attribution · Recommendation · Calibration · Brazilian Tax · Financial Reliability |
| [05 Engineering](docs/05-engineering/) | Repository · Coding Standards · Testing · Definition of Done · Technical Debt · Contributing |
| [06 Governance](docs/06-governance/) | Risk Register · AI Strategy · Compliance & Legal Posture |

---

## Disclaimer

Atlas is a personal analysis tool. **It is not financial, investment, tax, or legal advice.** All
figures are estimates produced from user-supplied data under stated assumptions. Financial decisions
are the user's own.
