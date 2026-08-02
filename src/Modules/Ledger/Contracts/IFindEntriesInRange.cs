using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Contracts;

// A second Open Host Service, added when Ingestion's fuzzy cross-source duplicate detection
// (FR-110) became the first real need for it — the same evolution IPostJournalEntry followed in
// M1 Slice 1. Returns Original entries only (never Reversal/Replacement correction artifacts) in a
// ValidTime range, for comparing a new proposal against what's already posted, regardless of source.
public interface IFindEntriesInRange
{
    Task<ImmutableArray<JournalEntryPosted>> FindOriginalsInRangeAsync(
        TenantId tenantId, ValidTime from, ValidTime to, CancellationToken cancellationToken);
}
