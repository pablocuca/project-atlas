using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;

namespace Atlas.Modules.Ledger.Application;

public sealed class OpenAccountHandler(IAccountRepository accounts)
{
    public async Task<Result<Account>> HandleAsync(
        TenantId tenantId,
        string code,
        string name,
        AccountType type,
        Commodity commodity,
        AccountId? parentId,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken)
    {
        var opened = Account.Open(tenantId, code, name, type, commodity, parentId, openedAt);
        if (opened.IsFailure)
            return opened;

        // INV-022: the database's UNIQUE (tenant_id, code) is the actual source of truth for
        // uniqueness — avoids a check-then-insert race between two concurrent opens of the same code.
        var inserted = await accounts.InsertAsync(opened.Value, cancellationToken);
        return inserted.IsFailure ? Result.Fail<Account>(inserted.Error) : opened;
    }
}
