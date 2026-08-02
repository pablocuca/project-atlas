using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

public sealed record PostJournalEntryResult(JournalEntry Entry, JournalEntryPosted PublishedEvent);

public sealed class PostJournalEntryHandler(IJournalEntryRepository entries)
{
    public async Task<Result<PostJournalEntryResult>> HandleAsync(
        TenantId tenantId,
        ValidTime validTime,
        DecisionTime decisionTime,
        DateTimeOffset currentTradingDayClose,
        string description,
        string sourceId,
        string idempotencyKey,
        ImmutableArray<Posting> postings,
        CancellationToken cancellationToken)
    {
        var created = JournalEntry.Create(
            tenantId, validTime, decisionTime, currentTradingDayClose, description, sourceId, idempotencyKey, postings);
        if (created.IsFailure)
            return Result.Fail<PostJournalEntryResult>(created.Error);

        var inserted = await entries.InsertAsync(created.Value, cancellationToken);
        if (inserted.IsFailure)
            return Result.Fail<PostJournalEntryResult>(inserted.Error);

        return Result.Ok(new PostJournalEntryResult(created.Value, LedgerEventMapping.ToPublishedEvent(created.Value)));
    }
}
