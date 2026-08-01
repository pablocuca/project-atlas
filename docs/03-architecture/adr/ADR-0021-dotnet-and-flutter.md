# ADR-0021 — .NET 10 backend, Dart/Flutter client

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO

## Context

The technology choice must be viable for twenty years, must suit a numerically-heavy domain, and
must be sustainable by a single maintainer whose existing depth is in .NET, Azure, and Flutter.

## Decision

**.NET 10 (LTS)** for the backend and simulation engine; **Dart/Flutter** for the client. C# for
all domain and infrastructure code.

## Rationale

- **Longevity.** .NET has a predictable LTS cadence and a strong backward-compatibility record — the
  most relevant property for a two-decade system.
- **Numerical performance.** `Span<T>`, SIMD intrinsics, value types, and low-allocation patterns
  make Monte Carlo simulation fast without dropping to native code.
- **Type system.** Records, non-nullable reference types, and (increasingly) discriminated-union
  patterns support the value-object-heavy domain model this specification demands.
- **Roslyn analysers** are what make several architectural invariants enforceable rather than
  aspirational — a genuine, and unusual, architectural argument for the platform.
- **Maintainer depth.** At N=1 over decades, existing expertise is an architectural property.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Kotlin/JVM | Excellent type system; strong ecosystem | No existing depth; weaker Azure-native tooling; no analyser equivalent for the invariants | Loses the enforcement story and the expertise |
| Go | Simple; fast; small containers | Weak type system for a value-object-heavy domain; generics still limited for this style | Poor fit for the domain model |
| Rust | Best performance and correctness guarantees | Steep cost for business logic; small talent pool; slower iteration | Correctness gains do not offset velocity loss here |
| Python (numerical) | Best scientific ecosystem | Weak typing for a large domain; poor long-term maintainability at this scale | Better as a future `atlas-sim` swap than as the whole backend |
| TypeScript / Node | One language front-to-back | Weaker numerical performance; less suitable for a heavy domain model | Worse on both axes that matter |

**Note:** `atlas-sim` is already an isolated container consuming immutable snapshots and producing
immutable artifacts. If the numerical work ever justifies the Python scientific stack, that
extraction is a rewrite of one process, not of the system ([Modular Monolith §8](../03-modular-monolith.md)).

## Consequences

### Positive
- One language for all server-side code; strong tooling and analysers.
- Excellent Azure integration where it is wanted, without lock-in where it is not.
- Simulation performance sufficient to run ensembles inside the compute budget.

### Negative — accepted costs
- The scientific/statistical library ecosystem is thinner than Python's. Mitigated by implementing
  the specific models needed (bootstrap, regime switching) directly, with golden-file tests against
  reference implementations.
- Two languages total (C# + Dart) rather than one.

## Reversal cost

**High for the backend, moderate for the client.** Mitigated by keeping the domain free of framework
dependencies (MR-1) — domain logic is portable in principle.

## Compliance

Analysers enforce MR-1..MR-10, banned types, and ambient-clock rules. Simulation performance is a
tracked NFR.

## References
[ADR-0013](ADR-0013-flutter-client.md) · [Coding Standards](../../05-engineering/02-coding-standards.md)
