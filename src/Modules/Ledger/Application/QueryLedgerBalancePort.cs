using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain;

namespace Atlas.Modules.Ledger.Application;

public sealed class QueryLedgerBalancePort(BalanceAtHandler handler) : IQueryLedgerBalance
{
    public Task<Money> QueryAsync(
        TenantId tenantId,
        Guid accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(tenantId, new AccountId(accountId), commodity, asOfValidTime, asOfDecisionTime, cancellationToken);
}
