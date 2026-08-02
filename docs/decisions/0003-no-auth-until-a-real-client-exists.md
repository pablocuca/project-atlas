# Decision 0003 — No authentication in M0's `Atlas.Host`

**Status:** Accepted · **Date:** 2026-08-02 · **Relates to:** [Security Strategy §3](../03-architecture/06-security-strategy.md), [Container & Component View](../03-architecture/02-container-and-component-view.md)

## Context

The container view shows OIDC authentication at the edge as part of the standing architecture, and
`docs/03-architecture/06-security-strategy.md` §3 names Passkey/WebAuthn as the target primary
authentication mechanism. Neither document ties this to a milestone. No `FR-` in
`docs/01-product/02-functional-requirements.md` requires an API or UI surface at M0 at all —
M0's own roadmap entry says the goal is "a correct, bitemporal, double-entry ledger with manual
entry. Nothing else." `Atlas.Host` exists in this slice purely to satisfy the M0 exit gate's
`docker compose up` requirement (NFR-609), not to serve a real client.

## Decision

M0's `Atlas.Host` ships with **no authentication or authorization**. Every `/ledger/*` route is
open to anyone who can reach the process.

This is safe only because of a hard constraint that comes with it: **this Host must never be
reachable beyond `localhost` or a private developer machine.** No Bicep, no cloud deployment, no
reverse proxy exposing it — those stay out of scope for exactly this reason, not merely because
they're unbuilt. Building Passkey/OIDC now would mean guessing at a shape with nothing to validate
it against: the Flutter client that would actually use it doesn't exist yet (ADR-0013), and
`security-strategy.md`'s target design assumes a real device/session flow this slice has no
counterpart for.

## Consequences

- Any future slice that makes `Atlas.Host` reachable from anywhere other than a developer's own
  machine — Bicep/Azure Container Apps deployment, a shared dev environment, anything — must
  implement real authentication first. That slice's plan should treat this decision as the gate it
  needs to clear, not a detail to remember.
- `docs/03-architecture/06-security-strategy.md`'s target auth design is not superseded by this
  decision — it's simply not built yet. When a real client (the Flutter app) exists to validate
  against, that's the trigger to build it, not a calendar date.
- NG-07 (single-tenant, no signup/billing machinery) already licenses skipping tenant-management
  complexity; this decision extends the same reasoning to authentication itself, for the same
  N=1-operator reason.
