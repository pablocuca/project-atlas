# Coding Standards

**Status:** Ratified · **Owner:** Staff Engineer

Standards that are **enforced** rather than encouraged. Anything in this document that is not
machine-checkable is a candidate for deletion — a guideline nobody can verify is a guideline nobody
follows in year three.

---

## 1. The non-negotiables

| # | Rule | Enforcement |
|---|---|---|
| CS-1 | No `double` or `float` in any financial path | Roslyn analyser, error |
| CS-2 | No `DateTime.Now` / `DateTimeOffset.Now` / `DateTime.Today` in `*.Domain` | Roslyn analyser, error |
| CS-3 | No `DbContext`, `HttpClient`, or file I/O outside `*.Infrastructure` | Roslyn analyser, error |
| CS-4 | Nullable reference types enabled, warnings as errors | `.editorconfig` |
| CS-5 | No `async void` except event handlers | Analyser |
| CS-6 | No silent `catch` — handle, wrap as a domain error, or let it propagate | Analyser + review |
| CS-7 | No primitive obsession in domain signatures — value objects only | Review |
| CS-8 | Public domain types are immutable (`record`, `init`, no setters) | Review |
| CS-9 | No vendor telemetry SDK — OTel only | Analyser |
| CS-10 | No `Money`, `Commodity`, or identifier value objects in log arguments | Analyser |

**CS-2 deserves emphasis.** Ambient clock access is the single easiest way to destroy AI-2 without
noticing. Time is always a parameter in domain code — every method that needs "now" receives it.

---

## 2. Domain code

```csharp
// ✅ Time as a parameter, value objects, immutable, invariant enforced at construction
public sealed record JournalEntry
{
    public EntryId Id { get; }
    public ValidTime ValidTime { get; }
    public DecisionTime DecisionTime { get; }
    public ImmutableArray<Posting> Postings { get; }

    private JournalEntry(/* … */) { /* … */ }

    public static Result<JournalEntry> Create(
        ValidTime validTime,
        DecisionTime decisionTime,          // ← passed in, never read from a clock
        ImmutableArray<Posting> postings)
    {
        if (postings.Length < 2)
            return Result.Fail<JournalEntry>(DomainError.EntryTooFewPostings);

        foreach (var group in postings.GroupBy(p => p.Money.Commodity))
            if (group.Sum(p => p.SignedMinorUnits()) != 0)
                return Result.Fail<JournalEntry>(DomainError.EntryUnbalanced(group.Key));  // BR-100

        return Result.Ok(new JournalEntry(/* … */));
    }
}
```

### Rules
- **Invariants live in constructors and factories**, never in services. An object that can exist in
  an invalid state will eventually be created in one.
- **`Result<T>` for expected failures; exceptions for bugs.** An unbalanced entry is expected input;
  a null aggregate is a bug.
- **Domain errors carry codes**, not just messages. Codes are stable and testable; messages are not.
- **No `virtual`, no inheritance** in the domain except explicitly modelled hierarchies. Composition
  by default.

---

## 3. Money

```csharp
// ✅
var total = Money.Sum(postings.Select(p => p.Money));       // commodity-checked
var (each, remainder) = total.DivideWithRemainder(3);       // remainder never discarded
var display = total.Round(Rounding.HalfEven);               // once, at the boundary

// ❌ every one of these fails the build or review
decimal total = 0; foreach (var p in postings) total += p.Amount;   // CS-1, CS-7
var each = total / 3;                                               // remainder lost, BR-004
var converted = brlAmount + usdAmount;                              // BR-002
```

Rounding is applied **once**, at a declared presentation or settlement boundary. Intermediate values
keep full precision. Rounding twice is a defect class that produces errors too small to notice and
too systematic to ignore.

---

## 4. Async and cancellation

- Every I/O method is `async` and takes a `CancellationToken` as its **last** parameter.
- `ConfigureAwait(false)` in libraries; not needed in the host.
- No `.Result`, no `.Wait()`, no `.GetAwaiter().GetResult()` — analyser error.
- No `Task.Run` in request paths. Background work goes through the outbox.

---

## 5. Persistence

- **Ledger writes bypass the ORM.** Raw parameterised SQL, explicit column lists. The write path is
  small, hot, and must be exactly understood.
- EF Core is permitted for non-ledger modules and read models.
- **No shared `DbContext` across modules.** Each module owns its context and its schema.
- Every migration is expand/contract. **No destructive down-migration on truth tables, ever.**
- All SQL is parameterised. Dynamic SQL construction is banned outright.

---

## 6. Errors and logging

```csharp
// ✅ structured, allow-listed fields, no financial data
logger.LogWarning("Reconciliation drift detected {SourceId} {DriftBucket} {CorrelationId}",
                  sourceId, driftBucket, correlationId);

// ❌ CS-10 — analyser error
logger.LogWarning("Balance mismatch: expected {Expected}", expectedMoney);
```

Allowed in logs: correlation ids, opaque entity ids, operation names, durations, error codes,
counts, coarse status, bucketed magnitudes.
**Never** in logs: monetary amounts, account numbers, CPF, institution names, goal names,
transaction descriptions, tokens.

---

## 7. Dart / Flutter

| Rule | Detail |
|---|---|
| **No business logic in the client** | It renders server-computed values. Any arithmetic beyond layout is a review block |
| No currency or number formatting logic | The server supplies locale-keyed, pre-formatted display strings with units (BR-B06) |
| **No string concatenation for user-facing text** | Full ICU messages only. Portuguese gender and number agreement break concatenated fragments in ways English never reveals (BR-B05) |
| Every user-facing string carries `lang` | Otherwise VoiceOver reads Portuguese with English phonemes (BR-B12) |
| Immutable state, unidirectional data flow | Riverpod, `freezed` models |
| Every widget with a number takes a typed view model | Never raw `double` |
| `flutter analyze` clean, warnings as errors | CI |
| Every card widget declares its `question` | Build-time validated |

**"No business logic in the client" is stricter than it sounds and is load-bearing.** If the client
computes anything, that computation is untraceable, untested by the rule-coverage gate, and
invisible to attribution. It also makes a future client rewrite (ADR-0013 reversal) cheap.

---

## 8. Comments

Comment **why**, never **what**. The code says what.

```csharp
// ✅ records a decision a reader could not infer
// Custo médio across all lots, not FIFO: Brazilian equities are averaged (INV-043).
// FIFO would produce wrong tax — the instinctive choice for a US-trained developer.
var basis = ComputeWeightedAverageBasis(lots);

// ❌ restates the code
// Compute the basis
var basis = ComputeWeightedAverageBasis(lots);
```

Comments citing `BR-`, `INV-`, or `ADR-` identifiers are encouraged — they are the navigable link
from code back to rationale, and they are what makes the codebase legible in 2034.

---

## 9. Formatting

`.editorconfig` is authoritative; `dotnet format` runs in CI. File-scoped namespaces, four-space
indent, 120-column soft limit, one type per file, `using` directives outside the namespace and
sorted. None of this is discussed in review — the formatter decides.

---

**See also:** [Testing Strategy](03-testing-strategy.md) · [Definition of Done](04-definition-of-done.md) · [ADR-0003](../03-architecture/adr/ADR-0003-integer-money.md)
