using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Contracts;

// The LedgerBalanceQuery OHS named in docs/02-domain/03-context-map.md R03 ("JournalEntryPosted
// stream + LedgerBalanceQuery"), added when Ingestion's reconciliation (FR-111) became the first
// real need for it — M0 Slice 2's decision explicitly deferred this until a downstream module
// existed to call it. AccountId is a bare Guid, not Ledger.Domain.AccountId, for the same reason
// every other Contracts type crosses the boundary that way.
public interface IQueryLedgerBalance
{
    Task<Money> QueryAsync(
        TenantId tenantId,
        Guid accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken);
}
