using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Domain.Entries;

public readonly record struct EntryId(Guid Value)
{
    public static EntryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public enum JournalEntryKind
{
    Original,
    Reversal,
    Replacement,
}

// JournalEntry: an atomic, balanced set of Postings representing one economic event. Immutable
// (INV-031, BR-101) — there is no method anywhere on this type that mutates an existing instance.
// Corrections are made by calling Correct(...), which returns two brand-new entries; this instance
// is never touched.
public sealed record JournalEntry
{
    public EntryId Id { get; }
    public TenantId TenantId { get; }
    public ValidTime ValidTime { get; }
    public DecisionTime DecisionTime { get; }
    public string Description { get; }
    public string SourceId { get; }
    public string IdempotencyKey { get; }
    public ImmutableArray<Posting> Postings { get; }

    // Decision 0001 §1: provenance pointer, not a unique key — a Reversal and its Replacement both
    // set this to the same original entry's id.
    public EntryId? CorrectsEntryId { get; }
    public JournalEntryKind Kind { get; }

    private JournalEntry(
        EntryId id,
        TenantId tenantId,
        ValidTime validTime,
        DecisionTime decisionTime,
        string description,
        string sourceId,
        string idempotencyKey,
        ImmutableArray<Posting> postings,
        EntryId? correctsEntryId,
        JournalEntryKind kind)
    {
        Id = id;
        TenantId = tenantId;
        ValidTime = validTime;
        DecisionTime = decisionTime;
        Description = description;
        SourceId = sourceId;
        IdempotencyKey = idempotencyKey;
        Postings = postings;
        CorrectsEntryId = correctsEntryId;
        Kind = kind;
    }

    // BR-109: manual entry is a first-class source with the same invariants as automated ingestion —
    // there is no privileged bypass of this factory for any source.
    public static Result<JournalEntry> Create(
        TenantId tenantId,
        ValidTime validTime,
        DecisionTime decisionTime,
        DateTimeOffset currentTradingDayClose,
        string description,
        string sourceId,
        string idempotencyKey,
        ImmutableArray<Posting> postings)
    {
        var validation = ValidatePostings(postings, idempotencyKey);
        if (validation.IsFailure)
            return Result.Fail<JournalEntry>(validation.Error);

        // BR-104: ValidTime may not exceed the current trading day's close. currentTradingDayClose is
        // supplied by the caller — Domain code never reads a clock (CS-2).
        if (validTime.Value > currentTradingDayClose)
            return Result.Fail<JournalEntry>(LedgerDomainErrors.EntryValidTimeInFuture);

        return Result.Ok(new JournalEntry(
            EntryId.New(), tenantId, validTime, decisionTime, description, sourceId, idempotencyKey,
            postings, correctsEntryId: null, JournalEntryKind.Original));
    }

    // Decision 0001: a correction produces exactly two new entries — a Reversal (exact negation of
    // this entry's postings) and a Replacement (the corrected postings) — both carrying ValidTime
    // equal to this entry's ValidTime (a restatement, not a new event) and CorrectsEntryId = Id.
    // This entry is never mutated: its own Kind and Postings are untouched by this call.
    public Result<(JournalEntry Reversal, JournalEntry Replacement)> Correct(
        DecisionTime correctionDecisionTime,
        string description,
        ImmutableArray<Posting> correctedPostings)
    {
        var reversalIdempotencyKey = $"{IdempotencyKey}:reversal:{correctionDecisionTime.Value:O}";
        var replacementIdempotencyKey = $"{IdempotencyKey}:replacement:{correctionDecisionTime.Value:O}";

        var validation = ValidatePostings(correctedPostings, replacementIdempotencyKey);
        if (validation.IsFailure)
            return Result.Fail<(JournalEntry, JournalEntry)>(validation.Error);

        var reversal = new JournalEntry(
            EntryId.New(), TenantId, ValidTime, correctionDecisionTime,
            $"Reversal of {Id}: {description}", SourceId, reversalIdempotencyKey,
            Postings.Select(p => p.Reversed()).ToImmutableArray(), Id, JournalEntryKind.Reversal);

        var replacement = new JournalEntry(
            EntryId.New(), TenantId, ValidTime, correctionDecisionTime,
            description, SourceId, replacementIdempotencyKey,
            correctedPostings, Id, JournalEntryKind.Replacement);

        return Result.Ok((reversal, replacement));
    }

    // For Infrastructure to rehydrate a row it already knows is valid — see Account.Reconstitute
    // for why this bypasses validation rather than re-running Create's checks.
    public static JournalEntry Reconstitute(
        EntryId id,
        TenantId tenantId,
        ValidTime validTime,
        DecisionTime decisionTime,
        string description,
        string sourceId,
        string idempotencyKey,
        ImmutableArray<Posting> postings,
        EntryId? correctsEntryId,
        JournalEntryKind kind) =>
        new(id, tenantId, validTime, decisionTime, description, sourceId, idempotencyKey, postings, correctsEntryId, kind);

    private static Result<Unit> ValidatePostings(ImmutableArray<Posting> postings, string idempotencyKey)
    {
        if (postings.Length < 2)
            return Result.Fail<Unit>(LedgerDomainErrors.EntryTooFewPostings);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result.Fail<Unit>(LedgerDomainErrors.EntryIdempotencyKeyRequired);

        // BR-100: every journal entry balances to zero per commodity. Multi-commodity entries are
        // permitted (INV-030) — each commodity's postings balance independently.
        foreach (var group in postings.GroupBy(p => p.Money.Commodity))
        {
            var net = group.Sum(p => p.SignedMinorUnits());
            if (net != 0)
                return Result.Fail<Unit>(LedgerDomainErrors.EntryUnbalanced(group.Key));
        }

        return Result.Ok(Unit.Value);
    }
}
