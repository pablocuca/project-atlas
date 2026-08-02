# Decision 0013 — Rule-coverage gate scoped to `BR-` identifiers cited in `src/`

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Testing Strategy §7](../05-engineering/03-testing-strategy.md),
TD-002 (debt register)

## Context

TD-002 tracked the rule-coverage gate specified in `docs/05-engineering/03-testing-strategy.md` §7 —
"Parse `docs/02-domain/05-business-rules.md` → set of `BR-` identifiers... fail the build on any
`BR-` with no test" — as a convention enforced by hand rather than by CI, with its own stated trigger
("before M1 adds Ingestion's or Positions' business rules") now fired: three modules' worth of rules
have shipped with no gate.

Two real implementation questions, neither settled by the spec text:

1. **Literally, the spec's catalog has ~130 `BR-` identifiers** spanning every future engine —
   Forecast, Taxation, Advisory, Calibration, Progression, Narrative, Localisation — almost none of
   which exist in code yet. A gate that fails the build on every `BR-` in the *full* catalog with no
   test would never be green until the whole decade-scale system is built, which cannot be the
   intended CI behaviour for M0–M1.
2. **How does "scan test assemblies" actually work at CI time?** The obvious approach — locate each
   test project's built `.dll` on disk and `Assembly.LoadFrom` it — risks dependency-resolution
   failures: a `.dll` loaded outside its own project's normal `dotnet test` process isn't guaranteed
   to resolve its own transitive NuGet dependencies (FsCheck, Npgsql, ...) correctly.

## Decisions

**Scope: `BR-` identifiers cited in `src/`, not the full spec catalog.** `RuleCoverageTests` scans
every `.cs` file under `src/` for `BR-nnn`/`BR-Xnn` citations (the same convention every rule-citing
comment in this codebase already follows) and requires *only those* to have a covering
`[BusinessRule("BR-nnn")]`-attributed test. A rule the current code doesn't yet implement can't fail
a gate about whether its implementation is tested. As new modules cite new `BR-`s, the gate's
in-scope set grows automatically — no manual catalog maintenance needed.

**Mechanism: compile-time `ProjectReference`, not runtime assembly loading.**
`Atlas.ArchitectureTests` references every test project that currently cites a `BR-` (`Atlas.Kernel.
Tests`, `Modules.Ledger.Domain.Tests`, `Modules.Ingestion.Domain.Tests`) and reflects over their
already-loaded `Assembly` objects via `typeof(SomeTestClass).Assembly` — the same pattern
`ModularMonolithTests` already uses for `Atlas.Modules.Ledger.Domain`. This sidesteps the dependency-
resolution risk entirely: a project reference means MSBuild/NuGet already resolved everything by the
time the test host runs. The cost is a small, explicit maintenance step — add a new `ProjectReference`
the first time a test project gains its first `[BusinessRule]` citation — documented at the
`ProjectReference` site itself.

**Matched by attribute name, not by a shared type.** Each test project defines its own copy of
`BusinessRuleAttribute` (already true for `Modules.Ledger.Domain.Tests` before this decision; now
also `Atlas.Kernel.Tests` and `Modules.Ingestion.Domain.Tests`). `RuleCoverageTests` matches
`attribute.AttributeType.Name == "BusinessRuleAttribute"` via `CustomAttributeData`, not by binding
to one shared type — avoiding a new cross-project reference just for a six-line marker attribute,
consistent with this codebase's existing preference for small, deliberate duplication over a shared
abstraction with only two or three consumers (Decision 0012 made the same call for a different
reason).

**A second check for typos**: every `BR-` id actually cited via `[BusinessRule]` must exist in the
canonical catalog — catches `[BusinessRule("BR-1080")]` or similar before it silently proves nothing.

## Consequences

- Retrofitted `[BusinessRule("BR-nnn")]` onto every test that was previously only comment-cited:
  `BR-002`/`BR-003`/`BR-004` in `MoneyTests` (Kernel), `BR-108` in `ReconcilerTests` (Ingestion).
  `BR-100`/`101`/`103`/`104`/`105`/`106`/`107`/`109` already had proper attributes in `Modules.Ledger.
  Domain.Tests` — this session's audit found the gate would have passed almost immediately once
  written, which is itself evidence the hand-tracking discipline TD-002 worried about eroding had, in
  fact, mostly held up.
- `INV-` identifiers are explicitly out of this gate's scope — Testing Strategy §7 only ever
  specified `BR-`. Ingestion's and Positions' rules are governed largely by `INV-` IDs
  (INV-040–043, INV-060, ...), which remain hand-tracked. A future decision can extend this same
  mechanism to `INV-` if that becomes the trigger; not done here to keep this gate's scope matching
  its spec exactly.
- Verified as a real gate, not a tautology: temporarily citing a fake `BR-999` in `src/Atlas.Kernel/
  Unit.cs` made the gate fail with a clear, actionable message before the citation was reverted.
