using System.Collections.Immutable;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

// The explicit boundary mapping from Domain's internal types to the published language. Kept here,
// not in Contracts, because Contracts must depend only on Atlas.Kernel (the module rule) and cannot
// see Domain's JournalEntry/Posting at all.
internal static class LedgerEventMapping
{
    public static JournalEntryPosted ToPublishedEvent(JournalEntry entry) => new(
        entry.Id.Value,
        entry.TenantId,
        entry.ValidTime,
        entry.DecisionTime,
        entry.Kind.ToString(),
        entry.CorrectsEntryId?.Value,
        entry.SourceId,
        entry.Description,
        entry.Postings.Select(ToPublishedPosting).ToImmutableArray());

    private static PostedPosting ToPublishedPosting(Posting posting) =>
        new(posting.AccountId.Value, posting.Money, posting.Direction.ToString());
}
