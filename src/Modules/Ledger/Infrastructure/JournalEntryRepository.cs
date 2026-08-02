using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;
using Npgsql;

namespace Atlas.Modules.Ledger.Infrastructure;

// Raw parameterised SQL, no ORM (docs/05-engineering/02-coding-standards.md §5). minor_units is
// always the signed value (Posting.SignedMinorUnits()) — debit-positive, matching Domain's
// LedgerReplay exactly, so SUM(minor_units) here and LedgerReplay.BalanceAt in Domain compute the
// same thing over the same data.
public sealed class JournalEntryRepository(NpgsqlDataSource dataSource) : IJournalEntryRepository
{
    public async Task<JournalEntry?> FindByIdAsync(TenantId tenantId, EntryId entryId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT entry_id, tenant_id, valid_time, decision_time, kind, corrects_entry, idempotency_key, source_id, description
            FROM ledger.journal_entry WHERE tenant_id = @tenantId AND entry_id = @entryId
            """);
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        command.Parameters.AddWithValue("entryId", entryId.Value);

        Guid id;
        DateTimeOffset validTime;
        DateTimeOffset decisionTime;
        string kind;
        Guid? correctsEntry;
        string idempotencyKey;
        string sourceId;
        string description;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            id = reader.GetGuid(0);
            validTime = reader.GetFieldValue<DateTimeOffset>(2);
            decisionTime = reader.GetFieldValue<DateTimeOffset>(3);
            kind = reader.GetString(4);
            correctsEntry = reader.IsDBNull(5) ? null : reader.GetGuid(5);
            idempotencyKey = reader.GetString(6);
            sourceId = reader.GetString(7);
            description = reader.GetString(8);
        }

        var postings = await FindPostingsAsync(new EntryId(id), cancellationToken);

        return JournalEntry.Reconstitute(
            new EntryId(id), tenantId, new ValidTime(validTime), new DecisionTime(decisionTime),
            description, sourceId, idempotencyKey, postings,
            correctsEntry is { } ce ? new EntryId(ce) : null, Enum.Parse<JournalEntryKind>(kind));
    }

    public async Task<Result<Unit>> InsertAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var entryCommand = new NpgsqlCommand(
                """
                INSERT INTO ledger.journal_entry
                    (entry_id, tenant_id, valid_time, decision_time, kind, corrects_entry, idempotency_key, source_id, description)
                VALUES (@entryId, @tenantId, @validTime, @decisionTime, @kind, @correctsEntry, @idempotencyKey, @sourceId, @description)
                """, connection, transaction))
            {
                entryCommand.Parameters.AddWithValue("entryId", entry.Id.Value);
                entryCommand.Parameters.AddWithValue("tenantId", entry.TenantId.Value);
                entryCommand.Parameters.AddWithValue("validTime", entry.ValidTime.Value);
                entryCommand.Parameters.AddWithValue("decisionTime", entry.DecisionTime.Value);
                entryCommand.Parameters.AddWithValue("kind", entry.Kind.ToString());
                entryCommand.Parameters.AddWithValue("correctsEntry", (object?)entry.CorrectsEntryId?.Value ?? DBNull.Value);
                entryCommand.Parameters.AddWithValue("idempotencyKey", entry.IdempotencyKey);
                entryCommand.Parameters.AddWithValue("sourceId", entry.SourceId);
                entryCommand.Parameters.AddWithValue("description", entry.Description);

                await entryCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var posting in entry.Postings)
            {
                await using var postingCommand = new NpgsqlCommand(
                    """
                    INSERT INTO ledger.posting (entry_id, account_id, commodity, minor_units, lot_ref)
                    VALUES (@entryId, @accountId, @commodity, @minorUnits, NULL)
                    """, connection, transaction);
                postingCommand.Parameters.AddWithValue("entryId", entry.Id.Value);
                postingCommand.Parameters.AddWithValue("accountId", posting.AccountId.Value);
                postingCommand.Parameters.AddWithValue("commodity", posting.Money.Commodity.Symbol);
                postingCommand.Parameters.AddWithValue("minorUnits", posting.SignedMinorUnits());

                await postingCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Ok(Unit.Value);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Fail<Unit>(LedgerApplicationErrors.DuplicateIdempotencyKey);
        }
    }

    public async Task<Money> BalanceAtAsync(
        TenantId tenantId,
        AccountId accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT COALESCE(SUM(p.minor_units), 0)::bigint
            FROM ledger.posting p JOIN ledger.journal_entry e USING (entry_id)
            WHERE e.tenant_id = @tenantId AND p.account_id = @accountId AND p.commodity = @commodity
              AND e.valid_time <= @asOfValidTime AND e.decision_time <= @asOfDecisionTime
            """);
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        command.Parameters.AddWithValue("accountId", accountId.Value);
        command.Parameters.AddWithValue("commodity", commodity.Symbol);
        command.Parameters.AddWithValue("asOfValidTime", asOfValidTime.Value);
        command.Parameters.AddWithValue("asOfDecisionTime", asOfDecisionTime.Value);

        var sum = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return Money.FromMinorUnits(sum, commodity);
    }

    private async Task<ImmutableArray<Posting>> FindPostingsAsync(EntryId entryId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT account_id, commodity, minor_units FROM ledger.posting WHERE entry_id = @entryId ORDER BY posting_id");
        command.Parameters.AddWithValue("entryId", entryId.Value);

        var builder = ImmutableArray.CreateBuilder<Posting>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var accountId = new AccountId(reader.GetGuid(0));
            var commodity = Commodity.BySymbol(reader.GetString(1));
            var minorUnits = reader.GetInt64(2);
            var direction = minorUnits >= 0 ? PostingDirection.Debit : PostingDirection.Credit;
            var money = Money.FromMinorUnits(Math.Abs(minorUnits), commodity);

            builder.Add(Posting.Create(accountId, money, direction).Value);
        }

        return builder.ToImmutable();
    }
}
