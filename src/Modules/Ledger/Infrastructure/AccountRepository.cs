using Atlas.Kernel;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Npgsql;

namespace Atlas.Modules.Ledger.Infrastructure;

// Raw parameterised SQL, no ORM (docs/05-engineering/02-coding-standards.md §5) — applied to reads
// as well as writes in this schema, for the same reason the standard gives for the write path: the
// ledger is small, hot, and must be exactly understood.
public sealed class AccountRepository(NpgsqlDataSource dataSource) : IAccountRepository
{
    private const string SelectColumns =
        "account_id, tenant_id, code, name, type, commodity, parent_id, opened_at, closed_at";

    public async Task<Account?> FindByIdAsync(TenantId tenantId, AccountId accountId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {SelectColumns} FROM ledger.account WHERE tenant_id = @tenantId AND account_id = @accountId");
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        command.Parameters.AddWithValue("accountId", accountId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<Account?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {SelectColumns} FROM ledger.account WHERE tenant_id = @tenantId AND code = @code");
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<Result<Unit>> InsertAsync(Account account, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO ledger.account (account_id, tenant_id, code, name, type, commodity, parent_id, opened_at, closed_at)
            VALUES (@accountId, @tenantId, @code, @name, @type, @commodity, @parentId, @openedAt, @closedAt)
            """);
        AddAccountParameters(command, account);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Ok(Unit.Value);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result.Fail<Unit>(LedgerApplicationErrors.AccountCodeAlreadyInUse);
        }
    }

    public async Task<Result<Unit>> UpdateClosedAtAsync(Account account, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE ledger.account SET closed_at = @closedAt WHERE tenant_id = @tenantId AND account_id = @accountId");
        command.Parameters.AddWithValue("closedAt", (object?)account.ClosedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("tenantId", account.TenantId.Value);
        command.Parameters.AddWithValue("accountId", account.Id.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected == 1
            ? Result.Ok(Unit.Value)
            : Result.Fail<Unit>(LedgerApplicationErrors.AccountNotFound);
    }

    private static void AddAccountParameters(NpgsqlCommand command, Account account)
    {
        command.Parameters.AddWithValue("accountId", account.Id.Value);
        command.Parameters.AddWithValue("tenantId", account.TenantId.Value);
        command.Parameters.AddWithValue("code", account.Code);
        command.Parameters.AddWithValue("name", account.Name);
        command.Parameters.AddWithValue("type", account.Type.ToString());
        command.Parameters.AddWithValue("commodity", account.Commodity.Symbol);
        command.Parameters.AddWithValue("parentId", (object?)account.ParentId?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("openedAt", account.OpenedAt);
        command.Parameters.AddWithValue("closedAt", (object?)account.ClosedAt ?? DBNull.Value);
    }

    private static Account Map(NpgsqlDataReader reader) => Account.Reconstitute(
        id: new AccountId(reader.GetGuid(0)),
        tenantId: new TenantId(reader.GetGuid(1)),
        code: reader.GetString(2),
        name: reader.GetString(3),
        type: Enum.Parse<AccountType>(reader.GetString(4)),
        commodity: Commodity.BySymbol(reader.GetString(5)),
        parentId: reader.IsDBNull(6) ? null : new AccountId(reader.GetGuid(6)),
        openedAt: reader.GetFieldValue<DateTimeOffset>(7),
        closedAt: reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));
}
