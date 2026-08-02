using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Contracts;

// The Open Host Service (R02, docs/02-domain/03-context-map.md: "EntryProposal -> PostJournalEntry.
// Never raw source rows") other modules use to post into the ledger in-process — no HTTP round-trip
// within the same host. Implemented by Ledger.Application, consumed by any module depending only on
// Ledger.Contracts (MR-2). Guid-keyed and Atlas.Kernel-typed, matching JournalEntryPosted's existing
// shape, for the same reason: a caller outside Ledger has no business seeing AccountId/EntryId.
public interface IPostJournalEntry
{
    Task<Result<JournalEntryPosted>> PostAsync(PostJournalEntryCommand command, CancellationToken cancellationToken);
}

public sealed record PostJournalEntryCommand(
    TenantId TenantId,
    ValidTime ValidTime,
    DecisionTime DecisionTime,
    DateTimeOffset CurrentTradingDayClose,
    string Description,
    string SourceId,
    string IdempotencyKey,
    ImmutableArray<PostingCommand> Postings);

public sealed record PostingCommand(Guid AccountId, Money Money, string Direction); // "Debit" | "Credit"
