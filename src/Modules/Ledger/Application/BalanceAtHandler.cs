using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;

namespace Atlas.Modules.Ledger.Application;

public sealed class BalanceAtHandler(IJournalEntryRepository entries)
{
    // INV-035: both time coordinates required, no single-time overload — the same rule
    // LedgerReplay.BalanceAt enforces in Domain; this is the Postgres-backed equivalent.
    public Task<Money> HandleAsync(
        TenantId tenantId,
        AccountId accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken) =>
        entries.BalanceAtAsync(tenantId, accountId, commodity, asOfValidTime, asOfDecisionTime, cancellationToken);
}
