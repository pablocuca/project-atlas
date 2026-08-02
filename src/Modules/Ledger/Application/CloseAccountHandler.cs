using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

public sealed class CloseAccountHandler(IAccountRepository accounts, IJournalEntryRepository entries)
{
    public async Task<Result<Account>> HandleAsync(
        TenantId tenantId, AccountId accountId, DateTimeOffset closedAt, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(tenantId, accountId, cancellationToken);
        if (account is null)
            return Result.Fail<Account>(LedgerApplicationErrors.AccountNotFound);

        var validTime = new ValidTime(closedAt);
        var decisionTime = new DecisionTime(closedAt);
        var currentBalance = await entries.BalanceAtAsync(
            tenantId, accountId, account.Commodity, validTime, decisionTime, cancellationToken);

        var closed = account.Close(closedAt, currentBalance);
        if (closed.IsFailure)
            return closed;

        var updated = await accounts.UpdateClosedAtAsync(closed.Value, cancellationToken);
        return updated.IsFailure ? Result.Fail<Account>(updated.Error) : closed;
    }
}
