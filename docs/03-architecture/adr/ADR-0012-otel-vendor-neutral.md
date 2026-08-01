# ADR-0012 — OpenTelemetry native, vendor-neutral backend

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** SRE

## Context

The brief names Application Insights and Azure Monitor. Application Insights bills per GB ingested,
which creates a perverse incentive: the richer and more useful the telemetry, the more it costs —
against a US$30/month total ceiling. Meanwhile, telemetry backends are the most commonly replaced
component in any long-lived system.

## Decision

Instrument with **OpenTelemetry exclusively** (traces, metrics, logs), export via OTLP, and treat
the backend as configuration. Ship with a free-tier OTLP-compatible backend (Grafana Cloud). No
vendor SDK appears anywhere in Atlas code.

## Rationale

- AI-3 applied to telemetry: instrumenting once and switching backends by config is the same
  portability argument that drove ADR-0004.
- Free-tier OTLP backends cover this volume entirely, preserving budget for compute.
- OTel is the industry standard and is where the ecosystem has converged; betting on it is low risk.
- Application Insights remains available later at zero code cost if deeper Azure correlation ever
  matters.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Application Insights SDK | Deep Azure integration; familiar | Per-GB pricing penalises rich telemetry; vendor SDK throughout the codebase | Cost model conflicts with the goal; lock-in for no gain |
| Self-hosted LGTM stack | Full control; no per-GB cost | Compute + storage cost exceeds the ceiling; operational toil | Violates the cost ceiling and adds toil at N=1 |
| Logs only, no traces | Cheapest | Cannot follow a value from import to displayed number — the one thing Atlas most needs | Insufficient for the traceability goal |

## Consequences

### Positive
- Backend is a one-line change; no Atlas code depends on any vendor.
- Cardinality discipline is forced by free-tier limits, which is healthy anyway.

### Negative — accepted costs
- Dashboards must be re-authored if the backend changes (instrumentation does not).
- Some Azure-native correlation (e.g. platform-level dependency maps) is unavailable.

## Reversal cost

**Very low.** OTLP endpoint configuration change.

## Compliance

Analyser bans vendor telemetry SDK references. All spans and metrics defined in
[Observability Strategy §2](../07-observability-strategy.md).

## References
[Observability Strategy](../07-observability-strategy.md) · [Infrastructure](../08-infrastructure.md)
