using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Contracts;

// A third Open Host Service, added when Cashflow's expense classification (FR-301) became the first
// real need to look up a single account's own details (code/name/type) rather than a balance or a
// range of entries — the same evolution IPostJournalEntry, IFindEntriesInRange, and
// IQueryLedgerBalance each followed. Type is a string, not Domain's AccountType enum, for the same
// reason Kind/Direction are strings on JournalEntryPosted: Contracts may depend only on Atlas.Kernel.
public interface IFindAccount
{
    Task<AccountSummary?> FindAsync(TenantId tenantId, Guid accountId, CancellationToken cancellationToken);
}

public sealed record AccountSummary(Guid Id, string Code, string Name, string Type, string Commodity);
