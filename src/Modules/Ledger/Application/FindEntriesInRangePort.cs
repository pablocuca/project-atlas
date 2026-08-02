using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;

namespace Atlas.Modules.Ledger.Application;

public sealed class FindEntriesInRangePort(IJournalEntryRepository entries) : IFindEntriesInRange
{
    public async Task<ImmutableArray<JournalEntryPosted>> FindOriginalsInRangeAsync(
        TenantId tenantId, ValidTime from, ValidTime to, CancellationToken cancellationToken)
    {
        var found = await entries.FindOriginalsInRangeAsync(tenantId, from, to, cancellationToken);
        return [.. found.Select(LedgerEventMapping.ToPublishedEvent)];
    }
}
