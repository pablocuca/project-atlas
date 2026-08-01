# Contributing Guide

**Status:** Ratified · **Owner:** CTO

Written for two audiences: the current maintainer after an eighteen-month absence, and a stranger
arriving in 2034. If either can follow this document unaided, it works.

---

## 1. Start here

```bash
git clone <repo> && cd atlas
docker compose up -d                       # postgres + azurite
dotnet run --project src/Atlas.Host
dotnet run --project tools/atlas-seed -- --years 10 --seed 42
```

```bash
cd app && flutter run
```

Target: running system in **≤ 30 minutes from a clean clone** (NFR-609). If it takes longer, that is
a bug in this document — fix the document, not just your environment.

**Read before writing code:**
1. [Vision](../00-foundation/01-vision.md) and [Mission](../00-foundation/02-mission-and-north-star.md) — what this is
2. [Product Philosophy](../00-foundation/03-product-philosophy.md) — the fourteen laws you will be judged against
3. [Ubiquitous Language](../00-foundation/05-ubiquitous-language.md) — the words you must use
4. [Bounded Contexts](../02-domain/02-bounded-contexts.md) — where things live
5. The ADR for whatever you are about to touch

---

## 2. Workflow

```
1. Find or write the story          Definition of Ready must pass
2. Read the governing document      If it doesn't exist, write it first (ADR-0019)
3. Branch                           feat/… or fix/…, ≤ 3 days
4. Write the test first             especially for any BR-
5. Implement
6. Run gates locally                ./scripts/gates.sh
7. Open the PR                      complete the template honestly
8. Review                           any Product Law is grounds to block
9. Squash merge
```

### The specification-first rule
If you are implementing something the specification does not describe, **stop and write the
specification**. This is not bureaucracy — it is where the thinking happens, and deferring it means
doing the thinking later, in code, where it is far more expensive to change.

---

## 3. Commits

```
<type>(<scope>): <subject>

<body — why, not what. Cite FR-/BR-/INV-/ADR- identifiers.>
```

Types: `feat` `fix` `docs` `refactor` `test` `perf` `chore` `adr`
Scopes: module names, or `kernel`, `infra`, `client`, `rulesets`, `docs`

```
feat(attribution): add sampled Shapley escalation path

Analytic first-order attribution exceeded the 2% residual invariant (INV-131)
on months with three or more interacting drivers. Escalates to Monte Carlo
Shapley with 200 permutation samples when the analytic residual fails.

Implements BR-201, BR-202. Pays down TD-007.
```

---

## 4. Pull request template

```markdown
## What and why

## Law check
- [ ] L1  Every new surface answers a stated question
- [ ] L2  No delta displayed without attribution
- [ ] L5  No LLM produces or adjusts a number
- [ ] L8  Uncertainty shown, not hidden
- [ ] L9  Nothing added that speaks without passing the Signal Gate
- [ ] L10 Nothing added that creates urgency to open the app
- [ ] L11 No outcome or return is gamified
- [ ] L12 All projections route through the tax engine
- [ ] L14 No instruction issued; options only
- [ ] N/A — no user-facing surface changed

## Requirements
FR: · BR: · INV: · ADR:

## Cost delta
Monthly Δ: US$ ___     ("unknown" is not accepted for infra changes)

## Definition of Done
- [ ] Every item in docs/05-engineering/04-definition-of-done.md
```

---

## 5. Review

The reviewer's job, in priority order:

1. **Is it correct?** Especially: money, time, tax, determinism.
2. **Does it violate a law or an invariant?** Block; no further justification needed.
3. **Does it erode a boundary?** The tests should catch it; verify they were not weakened.
4. **Will this be comprehensible in 2034?** Naming, comments, ADRs.
5. **Is it simple enough?** Complexity must be justified by a requirement, never by elegance.

**Not the reviewer's job:** formatting (the formatter decides), personal style preferences, or
suggesting a cleverer approach that is not measurably better.

At N=1, self-review is real review: open the PR, leave it overnight, review it as a stranger the
next morning. The overnight gap is not optional — it is what makes the second reading honest.

---

## 6. Writing an ADR

Write one when the decision was hard to make, expensive to reverse, or would surprise a newcomer.
Do **not** write one for obvious choices.

The **rejected alternatives** section is the point of the document. An ADR listing no alternatives
records a preference, not a decision, and will not help anyone in 2034.

To reverse a decision, write a **new** ADR that supersedes the old one. Never edit an accepted ADR's
decision — the history of wrong turns is more valuable than a tidy index.

---

## 7. Adding a module

1. ADR justifying a new bounded context — this is a significant act, not a routine one
2. Add the four projects (`Domain`, `Application`, `Infrastructure`, `Contracts`)
3. Implement `IAtlasModule`
4. Create the PostgreSQL schema and its restricted role
5. Register in the composition root
6. Add architecture tests for its boundaries
7. Update [Bounded Contexts](../02-domain/02-bounded-contexts.md) and [Context Map](../02-domain/03-context-map.md)

---

## 8. Adding a tax rule

1. **Never in code.** Rules are data (ADR-0017)
2. New ruleset version if the law changed; never edit an existing version
3. Cite the primary legislation with a verification date
4. Golden fixtures with **independently verified** expected outputs
5. Verify every prior ruleset still passes its own corpus
6. Review by a qualified Brazilian tax professional before production

---

## 9. Things that will get a PR rejected immediately

- Floating-point money
- `DateTime.Now` in domain code
- A cross-module database join
- A number in the UI without a unit
- A probability without a band
- An LLM computing anything
- A new alert without a runbook
- A business rule with no test
- A "temporary" boundary violation
- An infrastructure change with an unstated cost delta
- Imperative advice copy
- A gamification mechanic that can observe returns

---

**See also:** [Definition of Done](04-definition-of-done.md) · [Coding Standards](02-coding-standards.md) · [Repository Structure](01-repository-structure.md)
