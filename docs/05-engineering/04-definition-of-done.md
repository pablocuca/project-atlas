# Definition of Done

**Status:** Ratified · **Owner:** CTO

A checklist that a reviewer may block on, item by item, with no further justification required.
"Done" is not "it works on my machine" and not "the feature exists" — it is **every item below**.

---

## For every change

### Correctness
- [ ] Acceptance criteria from the story are met, verbatim
- [ ] Every affected `BR-` has a test annotated with its identifier
- [ ] Every affected `INV-` is enforced in code, not merely documented
- [ ] Property tests added where the change introduces an algebraic law
- [ ] Golden files added or updated where the change affects tax, adapters, or canonicalisation

### Architecture
- [ ] No new module boundary violation (MR-1..MR-10)
- [ ] No new dependency in `Atlas.Kernel` without an ADR
- [ ] No ambient clock, floating-point money, or cross-layer access introduced
- [ ] Any published-language change is versioned, with contract tests on both sides
- [ ] Any decision that was hard, costly to reverse, or surprising has an ADR

### Product laws
- [ ] **Law check completed in the PR template** — the reviewer may block on any law
- [ ] Any new surface states the question it answers
- [ ] Any new number carries its unit, uncertainty, and provenance
- [ ] Nothing introduced creates urgency to open the app
- [ ] Nothing introduced can alert on a stochastic driver

### Quality gates
- [ ] All twelve CI gates green
- [ ] Determinism gate passes
- [ ] Historical corpus test passes
- [ ] `no-frills build` passes
- [ ] Rule-coverage gate passes
- [ ] Copy lint passes, where copy changed

### Security & privacy
- [ ] No secret in code, config, image, or log
- [ ] No financial value or personal identifier reachable by a log or telemetry path
- [ ] New personal-data fields are field-level encrypted
- [ ] New dependencies have no critical CVE and are pinned

### Operability
- [ ] New failure modes emit a domain error with a code
- [ ] New async paths are idempotent and safe to retry
- [ ] New alerts have a runbook — **written first**
- [ ] Traces cover the new path end to end
- [ ] Cost delta stated in the PR (infrastructure changes)

### Documentation
- [ ] The governing document is updated in the same PR, not a follow-up
- [ ] New terms added to the [Ubiquitous Language](../00-foundation/05-ubiquitous-language.md)
- [ ] Commit message cites affected `FR-` / `BR-` / `INV-` identifiers

---

## Additional, for a milestone

- [ ] Every exit-gate criterion in the [Roadmap](../01-product/08-roadmap-and-milestones.md)
      objectively verified — not asserted
- [ ] Full export → clean restore → equality verified
- [ ] Restore drill performed and written up
- [ ] Technical debt register reviewed; nothing critical outstanding
- [ ] ADRs for the milestone reviewed for continued validity
- [ ] Specification library reviewed for drift against the implementation

---

## Additional, for a `ModelVersion` change

The most dangerous release type in the system.

- [ ] ADR describing the change and its expected effect on outputs
- [ ] Back-test against the historical snapshot corpus, with the output delta **quantified**
- [ ] Delta reviewed and accepted explicitly — not just observed
- [ ] `ModelChange` annotation appears in every affected trend (BR-204)
- [ ] Old artifacts remain valid and readable under their own version
- [ ] Calibration records segmented by model version — never silently pooled

---

## Additional, for a tax ruleset change

- [ ] Every new or changed rule cites primary legislation with a verification date
- [ ] Reviewed by a qualified Brazilian tax professional
- [ ] Golden corpus added for the new version
- [ ] **All prior ruleset versions still pass their own corpora**
- [ ] Effective-date continuity validated — no gaps, no overlaps
- [ ] Ambiguous cases resolve conservatively and raise `TaxAmbiguity`

---

## What "done" explicitly does not mean

| Not done | Why |
|---|---|
| "It works" | Working is the minimum, not the goal |
| "Tests pass" | Passing tests that assert nothing is the most common form of false confidence |
| "I'll document it later" | ADR-0019. Later does not arrive |
| "The gate is flaky, I'll merge anyway" | A flaky gate is a defect to fix, never to bypass |
| "It's temporary" | Temporary code becomes permanent code with a misleading comment |

---

**See also:** [Testing Strategy](03-testing-strategy.md) · [Contributing](06-contributing.md) · [Technical Debt Strategy](05-technical-debt-strategy.md)
