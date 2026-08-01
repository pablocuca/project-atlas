# Project Atlas — Specification Library

> **Financial Independence Operating System**
> Specification-first. No production code is written until the document governing it exists.

---

## How to read this library

This is not a wiki. It is a **layered specification** where each layer constrains the one below it.
A document may only contradict a document above it by way of an approved **ADR**.

```
FOUNDATION      why this exists, and what it refuses to be
      ↓
PRODUCT         what it must do, and how it must feel
      ↓
DOMAIN          the language, the model, the rules
      ↓
ARCHITECTURE    the shape of the system that satisfies the above
      ↓
ENGINES         the computational core: twin, forecast, attribution, advice
      ↓
ENGINEERING     how the code is written, tested, shipped, and paid for
      ↓
GOVERNANCE      risk, compliance, AI posture, decision history
```

**Rule of precedence.** Foundation > Product > Domain > Architecture > Engines > Engineering.
If an implementation is convenient but violates a Foundation invariant, the implementation is wrong.

---

## Locked decisions (2026-08-01)

| Decision | Choice | ADR |
|---|---|---|
| Jurisdiction | **Brazil only, deep** — behind an `IJurisdiction` seam | [ADR-0014](03-architecture/adr/ADR-0014-brazil-only-tax-engine.md) |
| Product boundary | **Single-tenant core, OSS-ready seams** | [ADR-0011](03-architecture/adr/ADR-0011-single-tenant-core.md) |
| Cloud run-rate ceiling | **≤ US$30/month** | [ADR-0015](03-architecture/adr/ADR-0015-cost-ceiling.md) |
| Advice posture | **Ranked options with quantified tradeoffs** | [ADR-0022](03-architecture/adr/ADR-0022-advice-posture.md) |
| Language | **Bilingual product (pt-BR default, en); English-only specification** | [ADR-0023](03-architecture/adr/ADR-0023-bilingual-product-english-spec.md) |

---

## 00 — Foundation

| Document | What it settles |
|---|---|
| [Vision](00-foundation/01-vision.md) | The ten-year picture and what "won" looks like |
| [Mission & North Star](00-foundation/02-mission-and-north-star.md) | The single question, formalised into a measurable quantity |
| [Product Philosophy](00-foundation/03-product-philosophy.md) | The fourteen laws every feature is judged against |
| [Non-Goals](00-foundation/04-non-goals.md) | What Atlas will never become, and why |
| [Ubiquitous Language](00-foundation/05-ubiquitous-language.md) | The glossary. Binding on code, UI, and docs |

## 01 — Product

| Document | What it settles |
|---|---|
| [Persona & Jobs to Be Done](01-product/01-persona-and-jobs.md) | Who this is for and what they are hiring it to do |
| [Functional Requirements](01-product/02-functional-requirements.md) | FR catalogue, traced to capabilities |
| [Non-Functional Requirements](01-product/03-non-functional-requirements.md) | NFR catalogue with measurable targets |
| [Quality Attributes](01-product/04-quality-attributes.md) | Attribute scenarios and their tactics |
| [Dashboard Strategy](01-product/05-dashboard-strategy.md) | Question-per-card doctrine, signal gating |
| [Gamification Strategy](01-product/06-gamification-strategy.md) | Progression system, and its anti-Goodhart constraints |
| [UX Architecture](01-product/07-ux-architecture.md) | Information architecture, surfaces, interaction laws |
| [Roadmap & Milestones](01-product/08-roadmap-and-milestones.md) | M0–M8, each with an exit gate |
| [Epics & Backlog](01-product/09-epics-and-backlog.md) | Epic tree, sized and sequenced |
| [User Stories & Acceptance Criteria](01-product/10-user-stories.md) | Gherkin-level detail for M0–M2 |
| [Localisation Strategy](01-product/11-localisation-strategy.md) | pt-BR / en boundary, three-tier term policy, per-locale linting |

## 02 — Domain

| Document | What it settles |
|---|---|
| [Event Storming](02-domain/01-event-storming.md) | The full event surface of the financial life |
| [Bounded Contexts](02-domain/02-bounded-contexts.md) | 17 contexts, classified core / supporting / generic |
| [Context Map](02-domain/03-context-map.md) | Relationships, ACLs, published languages |
| [Domain Model](02-domain/04-domain-model.md) | Aggregates, entities, value objects, invariants |
| [Business Rules](02-domain/05-business-rules.md) | Numbered, testable rule catalogue |

## 03 — Architecture

| Document | What it settles |
|---|---|
| [Architecture Vision](03-architecture/01-architecture-vision.md) | The Four Loops, and the system spine |
| [Container & Component View](03-architecture/02-container-and-component-view.md) | C4 L1–L3 |
| [Modular Monolith](03-architecture/03-modular-monolith.md) | Module boundaries and enforcement |
| [Data Strategy](03-architecture/04-data-strategy.md) | Bitemporal double-entry ledger, retention, portability |
| [Ingestion & Integration](03-architecture/05-ingestion-and-integration.md) | Adapters, idempotency, reconciliation |
| [Security Strategy](03-architecture/06-security-strategy.md) | Threat model, crypto, secrets, LGPD |
| [Observability Strategy](03-architecture/07-observability-strategy.md) | OTel, SLIs of the *system*, cost-aware telemetry |
| [Infrastructure](03-architecture/08-infrastructure.md) | Azure topology inside the US$30 ceiling |
| [DevOps & CI/CD](03-architecture/09-devops-and-cicd.md) | Pipelines, environments, promotion, rollback |
| [ADR Index](03-architecture/adr/README.md) | All architecture decision records |

## 04 — Engines

| Document | What it settles |
|---|---|
| [Digital Twin](04-engines/01-digital-twin.md) | State projection, snapshots, versioning |
| [Forecast Engine](04-engines/02-forecast-engine.md) | Stochastic model, artifacts, replay |
| [Simulation Engine](04-engines/03-simulation-engine.md) | Scenario algebra and counterfactuals |
| [Attribution Engine](04-engines/04-attribution-engine.md) | ΔFI decomposition — the anti-noise core |
| [Recommendation Engine](04-engines/05-recommendation-engine.md) | Policy space, ranking, tradeoff disclosure |
| [Calibration & Scoring](04-engines/06-calibration-and-scoring.md) | Proving the forecasts are honest |
| [Tax Engine — Brazil](04-engines/07-tax-engine-brazil.md) | The hardest and most defensible component |
| [Financial Reliability Model](04-engines/08-financial-reliability-model.md) | SLIs, SLOs, error budgets, incidents, runbooks |

## 05 — Engineering

| Document | What it settles |
|---|---|
| [Repository Structure](05-engineering/01-repository-structure.md) | Monorepo layout, ownership |
| [Coding Standards](05-engineering/02-coding-standards.md) | .NET and Dart conventions, enforced |
| [Testing Strategy](05-engineering/03-testing-strategy.md) | Pyramid, property tests, golden files, calibration tests |
| [Definition of Done](05-engineering/04-definition-of-done.md) | Non-negotiable exit criteria |
| [Technical Debt Strategy](05-engineering/05-technical-debt-strategy.md) | Debt register, interest, paydown policy |
| [Contributing Guide](05-engineering/06-contributing.md) | Workflow, review, commit and ADR conventions |

## 06 — Governance

| Document | What it settles |
|---|---|
| [Risk Register](06-governance/01-risk-register.md) | Ranked risks with owners and triggers |
| [AI Strategy](06-governance/02-ai-strategy.md) | Where LLMs are allowed, and where they are banned |
| [Compliance & Legal Posture](06-governance/03-compliance-and-legal-posture.md) | LGPD, advice boundary, disclaimers |

---

## Document conventions

- **Requirement IDs** are permanent. `FR-`, `NFR-`, `BR-`, `INV-`, `RISK-`, `SLO-`. Never renumber; deprecate instead.
- **Status** header on every document: `Draft | Reviewed | Ratified | Superseded`.
- **Every claim that becomes code must be traceable** — a requirement with no test is a wish.
- Documents live in the repo, version with the code, and change by pull request only.
