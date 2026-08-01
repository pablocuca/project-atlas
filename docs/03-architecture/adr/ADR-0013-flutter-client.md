# ADR-0013 — Flutter single codebase for iOS and web

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, UX Architect

## Context

Atlas needs a primary mobile surface (iOS, the user's ecosystem) and a secondary large-screen
surface for deep analysis — dense tables, multi-series charts, scenario comparison. The user is a
Flutter specialist, which is a genuine and legitimate input: at N=1, maintainer velocity over a
decade is an architectural property, not a preference.

## Decision

**Flutter, one codebase**, targeting iOS (primary) and web (secondary). Native platform channels
only where genuinely required (Keychain/Secure Enclave, biometrics, widgets).

## Rationale

- One codebase, one language, one state model, one test suite — sustainable for a single maintainer
  over decades.
- Flutter's rendering model suits Atlas's needs unusually well: the product is dominated by dense,
  custom, information-rich visualisations rather than platform-standard controls, so the usual
  "doesn't feel native" objection largely does not apply.
- Web target is free, and is the right surface for deep analysis views.
- Existing expertise materially reduces the risk of the client becoming the neglected half.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| SwiftUI + separate web app | Best-in-class iOS feel; native widgets | Two codebases, two languages, two test suites — the maintenance burden compounds over a decade | Unsustainable at N=1 |
| React Native | Large ecosystem | Weaker for dense custom charting; JS bridge overhead; no existing expertise | Strictly worse fit here |
| Web only (PWA) | One target; simplest | Weak iOS integration (widgets, biometrics, offline capture); poor quick-capture ergonomics | Mobile capture is core to keeping the ledger current |
| Native iOS only | Best mobile experience | No large-screen analysis surface | Deep analysis genuinely needs a big screen |

## Consequences

### Positive
- One client codebase for the project's lifetime.
- Custom visualisation components are written once and render identically everywhere.
- Offline capture on iOS keeps the ledger current, which protects data-quality SLIs.

### Negative — accepted costs
- Flutter web bundle size and initial load are worse than a hand-tuned web app. Acceptable for a
  secondary surface used deliberately.
- Some iOS platform features require plugin work or platform channels.
- Dependence on Flutter's long-term health — an ecosystem risk, recorded as `RISK-011`.

## Reversal cost

**Moderate.** The client is a presentation layer over a documented HTTP API; a native rewrite would
not touch the backend. This is itself a reason to keep all logic server-side.

## Compliance

No business logic in the client — it renders server-computed values. Enforced by review: any
arithmetic beyond layout in Dart is a review block.

## References
[UX Architecture](../../01-product/07-ux-architecture.md) · [ADR-0021](ADR-0021-dotnet-and-flutter.md)
