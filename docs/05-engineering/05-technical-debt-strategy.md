# Technical Debt Strategy

**Status:** Ratified · **Owner:** CTO

> Debt is a **financing decision**, not a moral failing. Atlas treats it the way it treats the
> user's liabilities: recorded explicitly, with a principal, an interest rate, and a paydown date.
> Unrecorded debt is the only kind that is unacceptable.

---

## 1. Taxonomy

| Type | Definition | Posture |
|---|---|---|
| **Deliberate, prudent** | "We'll ship the simple version and generalise when we know the shape" | ✅ Encouraged, if recorded |
| **Deliberate, reckless** | "No time for tests" | ❌ Never permitted here — correctness is the product |
| **Inadvertent, prudent** | "Now we understand the domain, we'd model it differently" | ✅ Normal; schedule the refactor |
| **Inadvertent, reckless** | "What's a bounded context?" | ⚠️ Prevented by architecture tests and review |

Atlas permits deliberate-prudent and expects inadvertent-prudent. The other two quadrants are
structurally blocked: reckless debt in a financial correctness system is not a trade-off, it is a
defect with a euphemism.

---

## 2. The debt register

`docs/06-governance/debt-register.md`, one entry per item:

```markdown
### TD-007 — Attribution uses analytic sensitivities only
**Incurred:** M4 · **Type:** Deliberate, prudent
**Principal:** ~3 days to implement sampled Shapley
**Interest rate:** MEDIUM — grows as driver interactions increase
**Symptom if unpaid:** residual exceeds 2% on multi-driver months; attribution rejected
**Trigger to pay:** residual invariant fails twice in one quarter
**Owner:** CTO · **Scheduled:** M5
```

Mandatory fields: **principal** (cost to fix), **interest rate** (rate of worsening), **symptom**
(observable consequence), **trigger** (the condition that forces payment).

Without a trigger, an entry is a wish. The trigger is what converts a note into a commitment.

---

## 3. Interest rates

| Rate | Meaning | Policy |
|---|---|---|
| **CRITICAL** | Compounds fast; blocks other work; risks correctness | Pay within the current milestone. Blocks the exit gate |
| **HIGH** | Grows steadily; will hurt within two milestones | Scheduled explicitly |
| **MEDIUM** | Stable; annoying but bounded | Pay when touching the area |
| **LOW** | Not growing; may never need paying | Record and leave. Deleting a LOW item is valid |

**A LOW item that has sat untouched for two years should be deleted, not paid.** Debt that never
accrues interest was not debt — it was a preference. Keeping it in the register creates false
anxiety and dilutes the register's signal.

---

## 4. Debt that may never be taken

| Prohibited | Why |
|---|---|
| Skipping tests for a business rule | Rule coverage is CI-gated; the build simply fails |
| Bypassing a module boundary "temporarily" | Architecture tests fail; temporary boundaries become permanent |
| Floating-point money "for now" | Destroys determinism irrecoverably |
| Mutable ledger writes | Violates the system of record; unrecoverable |
| Undocumented architectural decisions | ADR-0019; lost rationale is the decade-scale failure mode |
| Disabling the determinism gate | Corrupts every historical comparison, silently |
| Shipping a tax rule without a legal basis | Correctness and liability both |
| Deleting an artifact schema reader | Breaks the readable-forever promise permanently |

Each of these shares one property: **the damage is not recoverable by paying the debt later.** That
is the test for whether debt is permissible at all.

---

## 5. Paydown policy

| Rule | Detail |
|---|---|
| **Boy Scout rule** | Leave touched code better. Small improvements need no register entry |
| **20% allocation** | Roughly one working session in five goes to debt and toil reduction |
| **Milestone gate** | No CRITICAL debt may cross a milestone boundary |
| **Trigger-driven** | An item whose trigger fires is scheduled immediately, ahead of features |
| **Refactor separately** | Never mix a refactor and a behaviour change in one PR — it makes review impossible and bisection useless |

---

## 6. Detection

| Signal | Source |
|---|---|
| Architecture test failures | CI |
| Rising residual in attribution | Metric |
| Rising cyclomatic complexity | Analyser report |
| Repeated manual toil | The three-times-automate rule |
| Slowing CI pipeline | Metric — 25 min ceiling |
| Recurring incidents with the same root cause | Postmortem themes |
| "I'm afraid to change this" | The most reliable signal, and the only qualitative one worth trusting |

The last one deserves its place. Fear of changing code is the earliest and most accurate indicator
of accumulated debt, and it precedes every measurable signal.

---

## 7. Quarterly debt review

1. Recalculate every interest rate — did it grow as predicted?
2. Delete LOW items untouched for two years.
3. Promote items whose triggers fired.
4. Check the register against actual pain: is there suffering *not* in the register?
5. Verify no CRITICAL item is outstanding.

Item 4 is the important one. **A register that does not match lived pain is a fiction**, and the
review's real purpose is to detect that mismatch rather than to tidy the list.

---

**See also:** [Definition of Done](04-definition-of-done.md) · [Risk Register](../06-governance/01-risk-register.md)
