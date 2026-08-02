# Decision 0008 — Reconciliation tolerance is one major unit of the source's commodity

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [BR-108](../02-domain/05-business-rules.md), [FR-111](../01-product/02-functional-requirements.md), [US-012](../01-product/10-user-stories.md), [M1 exit gate](../01-product/08-roadmap-and-milestones.md)

## Context

BR-108 states that a reconciliation discrepancy "over tolerance" raises a data-quality breach, but
no document defines the tolerance's numeric value or how it should scale across commodities. The
only concrete number anywhere in the specs is the M1 exit gate's own prose: "reconciled to
≤ R$ 1,00." US-012's worked example (R$ 14.328,91 reported vs. R$ 14.290,00 ledger, a R$ 38,91
discrepancy) only proves that gap must breach — it's far larger than any plausible tolerance and
doesn't pin down the boundary itself.

Hardcoding `100` (R$1,00 in centavos) would silently break for any commodity whose minor-unit scale
isn't 2 — e.g. a future integer-scale commodity, or one with 3+ decimal places — despite `Commodity`
already carrying `MinorUnitScale` for exactly this kind of calculation.

## Decision

`Reconciler.Reconcile` treats "one major unit of the commodity" as the tolerance, computed as
`10 ^ MinorUnitScale` minor units — R$1,00 for BRL (scale 2), the same value the exit gate names,
but derived rather than hardcoded so it holds for any commodity Atlas ever adds. A discrepancy
`<= tolerance` reconciles; anything greater raises the breach.

## Consequences

- `ReconcileSourceHandler` never creates, corrects, or adjusts a ledger entry, in either outcome —
  it only computes and records the discrepancy (`ingestion.reconciliation`, insert-only, no
  `UPDATE` grant). BR-108's "never a silent adjustment" is a schema-level guarantee, not just an
  application-level one.
- If a future spec formalizes a different tolerance policy (e.g. proportional to balance, or a
  per-commodity override), this decision is superseded, not amended — `Reconciler.Reconcile` is the
  single seam to change.
- The `ReconciliationDrift` SLI named in US-012 is not implemented by this slice — recording the
  breach in `ingestion.reconciliation` is the durable fact an SLI would later read; wiring an actual
  SLI/alerting pipeline is deferred, consistent with M1's scope (no observability platform exists
  yet to emit it into).
