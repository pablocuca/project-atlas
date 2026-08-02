using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain;

namespace Atlas.Modules.Ledger.Application;

public sealed class FindAccountPort(IAccountRepository accounts) : IFindAccount
{
    public async Task<AccountSummary?> FindAsync(TenantId tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(tenantId, new AccountId(accountId), cancellationToken);
        return account is null ? null : new AccountSummary(account.Id.Value, account.Code, account.Name, account.Type.ToString(), account.Commodity.Symbol);
    }
}
