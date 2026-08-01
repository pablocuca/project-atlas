# Repository Structure

**Status:** Ratified · **Owner:** Principal Architect

Monorepo. One repository holds the specification, the backend, the client, the infrastructure, and
the tooling — because they version together, and a decision recorded in one place must be traceable
to the code it governs.

---

## Layout

```
atlas/
├─ README.md                        what this is, how to run it, in 30 minutes
├─ CONTRIBUTING.md                  → docs/05-engineering/06-contributing.md
├─ CLAUDE.md                        agent-facing project conventions
├─ .editorconfig                    formatting + analyser severity, enforced
├─ Directory.Build.props            shared build config + module reference guards
├─ Directory.Packages.props         central package version management
├─ atlas.sln
│
├─ docs/                            ◀ THE SPECIFICATION LIBRARY — read first
│  ├─ README.md                     the index
│  ├─ 00-foundation/  01-product/  02-domain/
│  ├─ 03-architecture/  └─ adr/
│  ├─ 04-engines/  05-engineering/  06-governance/
│  └─ decisions/                    decision log (non-architectural)
│
├─ contracts/                       published language schemas
│  ├─ events/                       *.json — domain event schemas, versioned
│  ├─ snapshots/                    twin snapshot schema, ALL versions
│  ├─ artifacts/                    forecast artifact schema, ALL versions
│  └─ api/                          OpenAPI
│
├─ rulesets/                        ◀ TAX RULES AS DATA, not code
│  └─ br/
│     ├─ 2024.1.0.yaml  2025.1.0.yaml  2026.1.0.yaml
│     └─ schema.json
│
├─ src/
│  ├─ Atlas.Kernel/                 Money, Commodity, bitemporal, TenantId. Depends on NOTHING
│  ├─ Atlas.Host/                   composition root, minimal APIs, migrator
│  ├─ Atlas.Sim/                    the simulation Job entry point
│  └─ Modules/
│     ├─ Ledger/        { Domain, Application, Infrastructure, Contracts }
│     ├─ Ingestion/     { … }
│     ├─ Positions/     { … }
│     ├─ MarketData/    { … }
│     ├─ Cashflow/      { … }
│     ├─ Liabilities/   { … }
│     ├─ HumanCapital/  { … }
│     ├─ Goals/         { … }
│     ├─ Taxation/      { Domain, Contracts }        ◀ pure — no Infrastructure project
│     ├─ Twin/          { … }
│     ├─ Forecast/      { … }
│     ├─ Attribution/   { … }
│     ├─ Calibration/   { … }
│     ├─ Advisory/      { … }
│     ├─ Reliability/   { … }
│     ├─ Progression/   { … }       ◀ removable (no-frills build)
│     ├─ Narrative/     { … }       ◀ removable (no-frills build)
│     ├─ Identity/      { … }
│     └─ Notification/  { … }
│
├─ tests/
│  ├─ Atlas.ArchitectureTests/      MR-1..MR-10 — one named test per rule
│  ├─ Atlas.DeterminismTests/       AI-2 gate
│  ├─ Atlas.HistoricalCorpusTests/  BR-A03 — every schema version, forever
│  ├─ Atlas.RuleCoverageTests/      every BR- has a test
│  ├─ Atlas.ContractTests/          every published language
│  ├─ Modules.*.Tests/              per-module unit + property tests
│  ├─ Atlas.IntegrationTests/
│  └─ fixtures/
│     ├─ golden/tax/                per ruleset version, independently verified
│     ├─ golden/adapters/           real, redacted source payloads
│     ├─ golden/canonicalisation/   cross-platform hash fixtures
│     ├─ corpus/snapshots/          one per schema version ever released
│     ├─ corpus/artifacts/          one per schema version ever released
│     └─ synthetic/                 atlas-seed output — a 10-year synthetic life
│
├─ app/                             Flutter client
│  ├─ lib/
│  │  ├─ core/                      design system, theming, formatting
│  │  ├─ features/                  mission_control, change_feed, operations,
│  │  │                             instruments, capture
│  │  ├─ data/                      API client, offline queue, cache
│  │  └─ main.dart
│  └─ test/
│
├─ infra/
│  ├─ main.bicep
│  ├─ modules/                      network, data, compute, security, observability, budget
│  └─ environments/                 dev.bicepparam, prod.bicepparam
│
├─ tools/
│  ├─ atlas-seed/                   synthetic financial life generator
│  ├─ atlas-export/                 full export + verification
│  ├─ copy-lint/                    banned imperative constructions
│  └─ ruleset-validate/             tax ruleset schema + continuity + legal basis
│
└─ .github/
   ├─ workflows/                    pr.yml, main.yml, weekly-verify.yml, drift.yml
   ├─ PULL_REQUEST_TEMPLATE.md      law check, cost delta, rule references
   └─ renovate.json
```

---

## Why these choices

### `docs/` is first, and is the primary artifact
Per [ADR-0019](../03-architecture/adr/ADR-0019-docs-precede-code.md). A reader arriving at this
repository should read the specification before the code. The directory ordering reflects that.

### `rulesets/` is data, outside `src/`
Tax rules are **content**, not code ([ADR-0017](../03-architecture/adr/ADR-0017-versioned-tax-rulesets.md)).
Keeping them outside `src/` makes that structural: they can be reviewed by a tax professional who
does not read C#, diffed meaningfully, and versioned independently of the application.

### `contracts/` holds every version, forever
Snapshot and artifact schemas from every release remain here permanently (BR-A03). Deleting one
breaks the historical corpus test. The directory only grows, and that is correct.

### `Taxation` has no `Infrastructure` project
The absence is the enforcement. A module with no infrastructure project **cannot** do I/O — purity
(INV-052) is expressed in the project graph rather than in a guideline.

### `Progression` and `Narrative` are ordinary modules
Nothing in the layout marks them as special, because the `no-frills build` proves their removability
mechanically (BR-804). Structural claims should be verified, not annotated.

### `fixtures/corpus/` only grows
One snapshot and one artifact per schema version ever released. It is the physical embodiment of the
promise that 2026 data remains readable in 2046.

---

## Ownership (CODEOWNERS)

At N=1 this is documentation of *intent*, and it becomes enforcement if the project opens.

```
/docs/00-foundation/     @cto          # Foundation changes need deliberate review
/docs/03-architecture/   @architect
/rulesets/               @domain @tax-reviewer
/src/Modules/Taxation/   @domain @tax-reviewer
/src/Atlas.Kernel/       @architect    # Kernel growth requires an ADR
/infra/                  @sre
/tests/Atlas.Architecture*/  @architect
```

---

## Naming conventions

| Element | Convention | Example |
|---|---|---|
| Project | `Atlas.Modules.{Context}.{Layer}` | `Atlas.Modules.Ledger.Domain` |
| Namespace | Matches project | `Atlas.Modules.Ledger.Domain.Entries` |
| Test | `{BRid}_{Behaviour}` where a rule applies | `BR_210_StochasticDriversNeverAlert` |
| Golden file | `{scenario}.{rulesetVersion}.golden.json` | `disposal-acoes.2026.1.0.golden.json` |
| ADR | `ADR-{nnnn}-{kebab-title}.md` | `ADR-0009-attribution-gated-alerting.md` |
| Migration | `{module}/{nnn}_{description}.sql` | `ledger/007_add_lot_ref_index.sql` |
| Ruleset | `{jurisdiction}/{year}.{major}.{minor}.yaml` | `br/2026.1.0.yaml` |

Test naming citing `BR-` identifiers is what makes the rule-coverage gate possible — the CI job
parses the business rules document and matches it against test attributes.

---

## Branch protection on `main`

- All twelve CI gates green
- Linear history, squash merge only
- No force push, no deletion
- Signed commits
- PR template completed, including the Law Check and cost delta

---

**See also:** [Coding Standards](02-coding-standards.md) · [Contributing](06-contributing.md) · [Modular Monolith](../03-architecture/03-modular-monolith.md)
