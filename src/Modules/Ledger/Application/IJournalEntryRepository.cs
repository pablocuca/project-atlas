using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

public interface IJournalEntryRepository
{
    Task<JournalEntry?> FindByIdAsync(TenantId tenantId, EntryId entryId, CancellationToken cancellationToken);

    // Result<Unit>, not void/throw: a duplicate (tenantId, sourceId, idempotencyKey) is BR-103's
    // expected outcome for a re-sent import, not a bug the caller should crash on.
    Task<Result<Unit>> InsertAsync(JournalEntry entry, CancellationToken cancellationToken);

    // INV-035: both time coordinates are mandatory — there is no single-time overload here either,
    // mirroring LedgerReplay.BalanceAt in Domain (this is the same query, against Postgres).
    Task<Money> BalanceAtAsync(
        TenantId tenantId,
        AccountId accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken);

    // Backs the IFindEntriesInRange port Ingestion uses for fuzzy cross-source duplicate detection
    // (FR-110) — the "recent entries" a new proposal gets compared against. Kind = Original only;
    // Reversal/Replacement rows are correction artifacts, not economic events to match against.
    Task<IReadOnlyList<JournalEntry>> FindOriginalsInRangeAsync(
        TenantId tenantId, ValidTime from, ValidTime to, CancellationToken cancellationToken);
}
