using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

public sealed record CorrectJournalEntryResult(
    JournalEntry Reversal,
    JournalEntry Replacement,
    JournalEntryPosted ReversalPublishedEvent,
    JournalEntryPosted ReplacementPublishedEvent);

public sealed class CorrectJournalEntryHandler(IJournalEntryRepository entries)
{
    public async Task<Result<CorrectJournalEntryResult>> HandleAsync(
        TenantId tenantId,
        EntryId originalEntryId,
        DecisionTime correctionDecisionTime,
        string description,
        ImmutableArray<Posting> correctedPostings,
        CancellationToken cancellationToken)
    {
        var original = await entries.FindByIdAsync(tenantId, originalEntryId, cancellationToken);
        if (original is null)
            return Result.Fail<CorrectJournalEntryResult>(LedgerApplicationErrors.EntryNotFound);

        var corrected = original.Correct(correctionDecisionTime, description, correctedPostings);
        if (corrected.IsFailure)
            return Result.Fail<CorrectJournalEntryResult>(corrected.Error);

        var (reversal, replacement) = corrected.Value;

        // Decision 0001: two new rows, never a mutation of the original. Both inserts must succeed
        // for the correction to be considered posted — if either idempotency key collides (BR-103),
        // the whole correction fails rather than leaving a reversal with no replacement.
        var reversalInserted = await entries.InsertAsync(reversal, cancellationToken);
        if (reversalInserted.IsFailure)
            return Result.Fail<CorrectJournalEntryResult>(reversalInserted.Error);

        var replacementInserted = await entries.InsertAsync(replacement, cancellationToken);
        if (replacementInserted.IsFailure)
            return Result.Fail<CorrectJournalEntryResult>(replacementInserted.Error);

        return Result.Ok(new CorrectJournalEntryResult(
            reversal, replacement,
            LedgerEventMapping.ToPublishedEvent(reversal), LedgerEventMapping.ToPublishedEvent(replacement)));
    }
}
