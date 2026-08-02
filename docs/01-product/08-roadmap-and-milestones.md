# Roadmap & Milestones

**Status:** Ratified · **Owner:** Product / CTO

> Milestones are sequenced by **dependency and risk retirement**, never by visible progress. Each
> has an **exit gate** that is objectively verifiable. A milestone is not done when the features
> exist; it is done when the gate passes.

---

## Sequencing principle

The highest-risk, least-reversible work goes first — even though it is the least visually
impressive. Specifically:

1. **The ledger comes before everything**, because every number in the system rests on it and its
   structure is effectively irreversible (ADR-0002).
2. **The tax engine comes before the forecast**, because a forecast that ignores tax is wrong by
   years and would produce a calibration record that must be discarded.
3. **Attribution comes before any notification**, because shipping notifications first would
   establish exactly the noisy behaviour the product exists to avoid.
4. **Calibration is registered from M4**, even though it cannot score anything for a year — because
   forecasts not registered at emission can never be scored retroactively.

The visually impressive parts — dashboards, options, narrative — come last, and deliberately so.

---

## M0 — Foundation *(the boring, irreversible part)*

**Goal:** a correct, bitemporal, double-entry ledger with manual entry. Nothing else.

| Scope | FRs |
|---|---|
| Kernel: `Money`, `Commodity`, bitemporal types | — |
| Chart of accounts, journal entries, postings | FR-101–103 |
| Bitemporal storage and as-of queries | FR-104–105 |
| Corrections by reversal + replacement | FR-106 |
| Module skeleton + architecture tests (MR-1..MR-10) | — |
| Local dev environment, CI pipeline, Bicep for dev | — |

**Exit gate**
- [x] 1,000 synthetic entries post, balance, and query correctly at arbitrary bitemporal coordinates
      — `tools/atlas-seed --years 10 --seed 42` posts 1,258 entries (22 corrected) and independently
      verifies 188 sampled balance queries against the live API on every CI run (`pr.yml`,
      `local-dev-smoke` job)
- [x] A correction to a 3-week-old entry preserves both the original belief and the corrected truth
      — `Modules.Ledger.Domain.Tests/LedgerReplayTests.cs`,
      `Atlas.IntegrationTests/LedgerPersistenceTests.cs` (real Postgres), and manually verified over
      real HTTP; the same property is what `atlas-seed`'s 22 corrections re-prove at scale on every push
- [x] Architecture tests pass; no module boundary violations — `Atlas.ArchitectureTests` (MR-1, MR-6)
      green in CI. Only these two are checkable with one module; MR-2/3/5/7/8/9/10 have no second
      module to violate yet (TD-005 in the [debt register](../06-governance/debt-register.md) tracks
      the remaining gates, not this one — module-boundary rules aren't gated by CI infrastructure,
      they're gated by module count)
- [x] Golden replay test: full ledger state reconstructible from the event log — proven as a property
      test at the Domain level (`Any_valid_entry_balances_to_zero_per_commodity` and friends) and at
      ~1,300-row scale via `atlas-seed`'s independent recomputation; no static golden-file fixture
      exists (none was ever specified for the ledger — see Slice 1 research notes — only Tax has one
      by design)
- [x] `docker compose up` → running system in ≤ 30 min from a clean clone (NFR-609) — the documented
      three-command sequence (`docker compose up -d`, `dotnet run --project src/Atlas.Host`,
      `dotnet run --project tools/atlas-seed`) runs in well under a minute total in CI; timed
      end-to-end, not just claimed

**Why first:** ADR-0002 is the deepest structural commitment in the system. Getting it wrong later
means rebuilding everything above it.

**Closure note (2026-08-02):** the exit gate is met. One scope-table item was *not* delivered:
**Bicep for dev**, deferred by [Decision 0003](../decisions/0003-no-auth-until-a-real-client-exists.md)
— provisioning cloud infrastructure for a host with no authentication would be the actual mistake,
not the missing infrastructure. It isn't part of the exit gate itself, so M0 is done per the gate;
Bicep is picked up whenever real authentication and a deployment target both exist. Full DoD
milestone review, including the technical debt register created by this review, recorded in
[Decision 0005](../decisions/0005-kernel-additions-m0.md) and the
[debt register](../06-governance/debt-register.md).

---

## M1 — Ingestion & positions

**Goal:** real data enters the system and reconciles. Positions and cost basis are correct.

| Scope | FRs |
|---|---|
| CSV import with column mapping; OFX | FR-107–108 |
| Idempotency, duplicate detection, human resolution | FR-109–110 |
| Raw payload archive; reconciliation | FR-111–112 |
| Lots, *custo médio*, instrument classification | FR-201–202 |
| Expense classification (Essential/Committed/Discretionary), versioned | FR-301 |

**Exit gate**
- [ ] Two years of real bank + broker history imported and reconciled to ≤ R$ 1,00
- [ ] Re-importing an overlapping statement window creates zero duplicates
- [ ] A deliberately corrupted CSV fails per-row without losing the batch or the raw payload
- [ ] Position quantities reconcile to ledger quantities exactly (INV-040)

---

## M2 — Market data, liabilities, export

**Goal:** valuation is possible; the mortgage is modelled; the data can leave.

| Scope | FRs |
|---|---|
| Marks, BCB series, valuation with staleness | FR-205–208 |
| Corporate actions and distributions | FR-203–204 |
| Loans: SAC/Price, indexation, rate resets, extra amortisation | FR-307–309 |
| Income modelling incl. 13th, bonus, FGTS | FR-306 |
| Goals with priority and flexibility | FR-401 |
| Mobile quick capture, offline | FR-113–114 |
| **Full export + verified re-import** | FR-930–931 |

**Exit gate**
- [ ] Portfolio valued at any historical date with correct staleness reporting
- [ ] A split and a bonus issue preserve total cost basis exactly
- [ ] Amortisation schedule matches the lender's statement to the centavo for 12 months
- [ ] **Export → clean restore → full equality, run in CI** (NFR-704)
- [ ] Expense captured on mobile in ≤ 10 s, offline, syncing correctly

**Why export this early:** the exit path must be proven before there is enough data for its absence
to become frightening. A disaster-recovery mechanism built late is a mechanism nobody trusts.

---

## M3 — Tax engine & first forecast

**Goal:** the two hardest components. The first honest FI number.

| Scope | FRs |
|---|---|
| `ITaxJurisdiction`, versioned rulesets, rule traces | FR-501–503, 511–512 |
| Come-cotas, exemptions, FII treatment | FR-504–506 |
| Tax drag projection | FR-509 |
| Spending model fitting, personal inflation | FR-303–305 |
| Human capital with `marketBeta`, employment hazard | FR-310–311 |
| Policy and IPS declaration | FR-402–403, 405 |
| Twin snapshot, materiality gate | FR-601–604 |
| Forecast engine: ensemble, distributions, convergence | FR-605–614 |

**Exit gate**
- [ ] Tax golden corpus passes for every regime, verified against independent manual computation
- [ ] Ruleset validation: no gaps or overlaps in effective dates; every rule cites a legal basis
- [ ] **Determinism gate: identical key ⇒ bit-identical artifact, across machines** (NFR-102)
- [ ] Forecast runs in ≤ 90 s, convergence verified, under-converged runs rejected
- [ ] First `P(FI by target)` produced, after tax, in real terms, with a band
- [ ] Job-loss-in-crash scenario shows a materially worse tail than independent job loss — proving
      the correlation is actually modelled

**This is the highest-risk milestone.** Two core contexts, both hard, both irreplaceable.

---

## M4 — Attribution, reliability, Mission Control

**Goal:** the product becomes itself. Signal, not noise.

| Scope | FRs |
|---|---|
| Shapley attribution, driver classification, residual discipline | FR-701–706 |
| **Signal Gate** | FR-707–709 |
| Controllable Drift, driver ranking | FR-710–711 |
| SLI catalogue, error budgets, Discretionary Error Budget | FR-801–803 |
| Burn-rate alerting, incidents, runbooks, postmortems | FR-804–806 |
| Health Score with breakdown | FR-807–808 |
| Mission Control, Change Feed, Operations surfaces | FR-901–903, 907 |
| Bilingual surface: locale context, server-side formatting, ICU catalogue | FR-940–942 |
| **Forecast registration for future scoring** | FR-820 |
| Scenario library and comparison | FR-615–616 |

**Exit gate**
- [ ] Attribution residual ≤ 2% across 100 historical delta pairs
- [ ] **Signal Gate suppresses ≥ 90% of daily deltas** over a 90-day backtest
- [ ] Zero notifications generated from stochastic-only deltas (BR-210), verified structurally
- [ ] Every Mission Control card states its question **in both locales**; build fails without it
- [ ] ICU completeness gate green; no message key missing in either locale
- [ ] Layouts hold at maximum Dynamic Type in pt-BR (the worst case) and en
- [ ] Every displayed number traceable to its artifact in one interaction
- [ ] Notification count over a 90-day simulation ≤ 4/month (NFR-808)

**The 90% suppression gate is the single most important acceptance criterion in the roadmap.** It
is the objective test of whether Atlas is Mission Control or an anxiety machine.

---

## M5 — Advisory & decision support

**Goal:** answer "what should I do next?" without instructing.

| Scope | FRs |
|---|---|
| Policy space enumeration | FR-810 |
| Counterfactual evaluation, common random numbers | FR-811–812 |
| Pareto ranking, preference ordering | FR-813 |
| 2–5 options with mandatory disclosure | FR-814–815 |
| After-tax option cost; noise-aware reporting | FR-816–817 |
| Option → Policy loop closure | FR-818 |
| PGBL/VGBL and withdrawal sequencing evaluation | FR-507, 510 |
| Ledger explorer, scenario comparison surfaces | FR-904–905 |
| LGPD cryptographic erasure | FR-932 |

**Exit gate**
- [ ] Copy lint passes **in both locales**: zero imperative advice constructions in any option text
- [ ] Portuguese banned lists exist for every category; `Ranked Option` renders as *Opção Avaliada*
- [ ] Every option carries a non-empty `notModelled` list, generated from real model gaps
- [ ] No option names a specific security
- [ ] Options within Monte Carlo noise report "no detectable difference"
- [ ] An accepted option creates a Policy version citing its `OptionId`
- [ ] Erasure runbook drilled successfully on a synthetic tenant

---

## M6 — Calibration, progression, narrative

**Goal:** the system starts grading itself. The peripheral layers arrive last.

| Scope | FRs |
|---|---|
| Brier, CRPS, PIT scoring; per-band reporting | FR-821–823 |
| Reliability status and the advice gate | FR-824 |
| Drift detection → model review | FR-825 |
| Reliability displayed with the North Star | FR-826 |
| Calibration diagrams | FR-906 |
| Progression: Discipline Index, streaks, XP, levels | FR-910–911 |
| Narrative over validated FactSets | FR-920–922 |

**Exit gate**
- [ ] ≥ 60 resolved 1-month claims scored; reliability diagram produced
- [ ] Insufficient-sample bands report `Unknown`, never a default-good value
- [ ] Degraded reliability demonstrably suppresses the correct option classes, with a stated reason
- [ ] **`no-frills build` passes** — Progression and Narrative removed, all financial tests green
- [ ] Zero untraceable numeric tokens across the narrative golden corpus
- [ ] Perverse-incentive audit completed and signed off for every progression mechanic

**Note on M6 timing:** calibration cannot produce meaningful output until roughly a year after M4
(when forecast registration began). The subsystem is built here; it *matures* on its own schedule.
That is expected and is stated up front so that its early emptiness is not mistaken for failure.

---

## M7 — Depth & automation

**Goal:** reduce toil; deepen the hard parts.

| Scope | FRs |
|---|---|
| *Nota de corretagem* parsing; B3 exports | FR-115–116 |
| Loss carry-forward; DARF worksheet | FR-508, 513 |
| Foreign holdings and FX | FR-210 |
| Risk tolerance assessment | FR-408 |
| User-posed counterfactuals | FR-617 |
| Aggregator integration *(cost-gated)* | FR-117 |

**Exit gate**
- [ ] Coverage SLI ≥ 95% sustained for 3 months
- [ ] Manual data entry time ≤ 15 min/month
- [ ] Aggregator decision made explicitly on cost/benefit, with the decision recorded either way

---

## M8 — Longevity

**Goal:** prove the system survives its own decade.

| Scope |
|---|
| Second full year of calibration data; model review cycle |
| Open-source readiness assessment (tenancy seam activation costed) |
| Documentation completeness audit against the specification library |
| Full DR drill: rebuild from export onto non-Azure infrastructure |
| Technical debt paydown to zero critical items |

**Exit gate**
- [ ] Restore from export onto a non-Azure host, verified equal
- [ ] Every ADR reviewed for continued validity; superseded where appropriate
- [ ] Calibration verified across 1m–1y bands
- [ ] Zero critical technical debt items outstanding

---

## Deliberately unscheduled

| Item | Condition to schedule |
|---|---|
| Multi-tenancy activation | A genuine second user exists |
| Second jurisdiction | A genuine need exists |
| Microservice extraction | A module meets ≥ 2 of the five criteria |
| Real-time anything | A use case where a sub-daily decision changes `t_FI` |
| Python numerical stack for `atlas-sim` | Model complexity genuinely exceeds .NET's practical ceiling |

---

## What is explicitly not on this roadmap

**Dates.** This is a single-maintainer project running alongside a career. Committing to calendar
dates would either be fiction or would create pressure to skip exit gates — and the exit gates are
the only thing protecting the project's quality bar. Milestones are sequenced; they are not
scheduled.

The one temporal constraint that is real: **M6 calibration cannot mature until ~12 months after M4
completes.** Everything else is dependency-ordered, not time-ordered.

---

**See also:** [Epics & Backlog](09-epics-and-backlog.md) · [Functional Requirements](02-functional-requirements.md) · [Risk Register](../06-governance/01-risk-register.md)
