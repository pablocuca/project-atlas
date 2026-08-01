# Quality Attributes

**Status:** Ratified · **Owner:** CTO / Principal Architect

Quality attribute **scenarios** in the SEI/ATAM form: a stimulus, an environment, and a measurable
response. Each names the architectural **tactic** that delivers it, so the connection between a
quality goal and a design decision is explicit rather than assumed.

---

## Priority order

When attributes conflict, resolve in this order. This ordering is itself an architectural decision
and differs sharply from a typical consumer application.

```
1. Correctness       6. Cost
2. Data durability   7. Usability
3. Security          8. Performance
4. Portability       9. Availability
5. Maintainability
```

Availability last is deliberate: a two-hour outage costs this product's user nothing, while a wrong
number costs them the product's entire reason to exist.

---

## QA-1 — Correctness

> **Scenario.** A broker files a corrected trade 21 days after the original. The system must
> incorporate the correction without altering the record of what was previously believed, and every
> downstream figure must remain reproducible in both readings.
>
> **Response measure:** both readings queryable; zero mutation of the original; all affected
> projections rebuilt within 5 minutes; the resulting delta classified as `Restatement`, not as a
> life change.

**Tactics:** bitemporal storage (ADR-0002) · append-only with reversal-and-replacement · projections
rebuildable from truth (AI-1) · `Restatement` driver class.

---

> **Scenario.** The same forecast is re-run eight years later on different hardware, a different
> .NET version, and a different CPU architecture.
>
> **Response measure:** bit-identical artifact. Any deviation is SEV-1 and blocks release.

**Tactics:** content addressing (ADR-0006) · integer money (ADR-0003) · counter-based RNG per path ·
canonical serialisation · no ambient clock (analyser-enforced) · CI determinism gate.

---

> **Scenario.** A tax rule changes retroactively, effective from a date already covered by stored
> forecasts.
>
> **Response measure:** new ruleset version created; historical forecasts continue to replay under
> their original ruleset; new forecasts use the new one; the difference is labelled `ModelChange`,
> never presented as a change in the user's situation.

**Tactics:** versioned rulesets with effective dates (ADR-0017) · pure tax module · `ModelChange`
classification (BR-204).

---

## QA-2 — Data durability

> **Scenario.** The Azure subscription is cancelled without warning.
>
> **Response measure:** complete restoration onto any container host within one day, from the most
> recent weekly export held outside Azure. Zero loss of ledger truth, snapshots, or artifacts.

**Tactics:** weekly full export to a user-controlled location · open formats only · CI-verified
export→restore→compare · no vendor-specific data format anywhere (AI-3).

---

> **Scenario.** A developer error attempts to delete ledger rows.
>
> **Response measure:** the operation fails at the database. No application code path exists that
> could succeed.

**Tactics:** per-module DB roles without `DELETE` on truth tables · no deletion API (BR-A02) ·
type-level absence of a delete operation.

---

## QA-3 — Security

> **Scenario.** The production database is exfiltrated in full.
>
> **Response measure:** CPF, account numbers, institution identifiers, source tokens, and free-text
> descriptions are unreadable. Structural data (amounts, dates, relationships) is exposed and is
> accepted as residual risk.

**Tactics:** field-level AES-256-GCM with tenant-scoped keys · keys in Key Vault, never in the DB ·
managed identity only.

**Residual risk accepted and recorded:** aggregate financial magnitude is inferable from structural
data alone. Mitigating that would require full homomorphic encryption or client-side-only
computation, both of which would make the forecast engine impossible. Stated rather than hidden.

---

> **Scenario.** A malicious string is embedded in a transaction description from an imported source,
> attempting prompt injection.
>
> **Response measure:** the string never reaches the model. The `FactSet` contains only numeric facts
> and enumerated labels; free text is excluded by default and delimited-and-marked where included.

**Tactics:** `FactSet` minimisation (BR-906) · Narrative module dependency isolation (MR-9) · no tool
access for the narration model.

---

## QA-4 — Portability

> **Scenario.** PostgreSQL must be replaced, or the system must move off Azure entirely.
>
> **Response measure:** no domain code changes. Repository implementations and Bicep change.
> Migration completes within days.

**Tactics:** ports and adapters (MR-1..MR-3) · no vendor SDK in domain or application layers ·
open export formats · blob used as a dumb byte store.

---

## QA-5 — Maintainability

> **Scenario.** A maintainer returns after 18 months away and must add a new tax regime.
>
> **Response measure:** they locate the ruleset schema, the golden corpus, and the governing ADR
> within 15 minutes using only the in-repo documentation. The change is additive and touches one
> module.

**Tactics:** docs-as-code (ADR-0019) · stable requirement identifiers · module isolation ·
ADRs recording rationale and rejected alternatives · rule-coverage gate keeping docs honest.

---

> **Scenario.** Someone attempts, under time pressure, to have the Progression module read portfolio
> value.
>
> **Response measure:** compile error. The dependency does not exist and cannot be added without
> visibly editing an architecture test.

**Tactics:** MR-8 · architecture tests · project reference guards.

---

## QA-6 — Cost

> **Scenario.** Simulation workload doubles as scenario coverage expands.
>
> **Response measure:** monthly run-rate remains ≤ US$30. Container Apps Job consumption scales with
> use and to zero at rest.

**Tactics:** scale-to-zero compute (ADR-0015) · content-addressed simulation caching · variance
reduction reducing required path counts · budget alerts at 50/80/100%.

---

## QA-7 — Usability

> **Scenario.** The user has 45 seconds and wants to know whether anything needs their attention.
>
> **Response measure:** Mission Control loads in ≤ 500 ms warm and answers definitively. If nothing
> needs attention, that is stated affirmatively, not as an empty state.

**Tactics:** six-card ceiling · question-per-card · empty-as-success (UX-7) · Signal Gate ensuring
the answer is usually "nothing".

---

> **Scenario.** The user distrusts a displayed probability and wants to know where it came from.
>
> **Response measure:** the full chain — components → attribution → artifact → snapshot → ledger — is
> reachable in five taps, each level meaningful on its own.

**Tactics:** universal drill-down spine (UX-2) · `factRefs` on every card · content-addressed
artifacts making provenance exact rather than approximate.

---

## QA-8 — Performance

> **Scenario.** A forecast runs with 50,000 paths over 660 monthly steps, computing Brazilian tax at
> each step — roughly 33 million tax computations.
>
> **Response measure:** ≤ 90 s on 2 vCPU, within the Job cost budget.

**Tactics:** integer arithmetic · struct path state, no per-step allocation · precomputed rate tables
per ruleset version · memoised lot classification · fast paths for the common no-event month ·
deterministic parallelism.

---

## QA-9 — Availability

> **Scenario.** `atlas-api` is unavailable for 90 minutes.
>
> **Response measure:** no financial consequence. Offline capture continues; queued facts sync on
> recovery; no decision has a deadline shorter than a day.

**Tactics:** offline capture · cached read state with explicit staleness · 99% SLO deliberately
chosen over 99.9% at ~5× the cost.

---

## Attribute conflicts, resolved in advance

| Conflict | Resolution | Recorded in |
|---|---|---|
| Correctness vs performance | Correctness. Reject under-converged runs rather than present them | BR-309 |
| Portability vs cost | Portability. Postgres at US$14 over free Azure SQL | ADR-0004 |
| Availability vs cost | Cost. 99% not 99.9% | ADR-0015 |
| Usability vs transparency | Transparency, layered. Simple surface, full depth on demand | UX-2 |
| Maintainability vs velocity | Maintainability. "Never optimise for speed" | ADR-0019 |
| Security vs usability | Security, but passwordless — passkeys are both more secure *and* easier | NFR-401 |
| Richness of advice vs legal exposure | Ranked options with disclosure | ADR-0022 |
| Engagement vs mission | Mission. Engagement is an anti-metric | NG-11 |

---

**See also:** [Non-Functional Requirements](03-non-functional-requirements.md) · [Architecture Vision](../03-architecture/01-architecture-vision.md) · [ADR Index](../03-architecture/adr/README.md)
