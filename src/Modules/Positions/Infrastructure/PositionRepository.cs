using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Positions.Application;
using Atlas.Modules.Positions.Domain;
using Npgsql;

namespace Atlas.Modules.Positions.Infrastructure;

public sealed class PositionRepository(NpgsqlDataSource dataSource) : IPositionRepository
{
    public async Task<Position?> FindAsync(TenantId tenantId, Commodity commodity, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        Guid positionId;
        Commodity costCommodity;
        decimal quantity;
        Money costBasis;

        await using (var command = new NpgsqlCommand(
            """
            SELECT position_id, cost_commodity, quantity, cost_basis_minor_units
            FROM positions.position WHERE tenant_id = @tenantId AND commodity = @commodity
            """, connection))
        {
            command.Parameters.AddWithValue("tenantId", tenantId.Value);
            command.Parameters.AddWithValue("commodity", commodity.Symbol);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            positionId = reader.GetGuid(0);
            costCommodity = Commodity.BySymbol(reader.GetString(1));
            quantity = reader.GetDecimal(2);
            costBasis = Money.FromMinorUnits(reader.GetInt64(3), costCommodity);
        }

        var lots = ImmutableArray.CreateBuilder<Lot>();
        await using (var command = new NpgsqlCommand(
            "SELECT lot_id, quantity, unit_cost_minor_units, acquired_at, source_entry_id FROM positions.lot WHERE position_id = @positionId",
            connection))
        {
            command.Parameters.AddWithValue("positionId", positionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lots.Add(new Lot(
                    new LotId(reader.GetGuid(0)), reader.GetDecimal(1),
                    Money.FromMinorUnits(reader.GetInt64(2), costCommodity),
                    reader.GetFieldValue<DateTimeOffset>(3), reader.GetGuid(4)));
            }
        }

        var disposals = ImmutableArray.CreateBuilder<Disposal>();
        await using (var command = new NpgsqlCommand(
            "SELECT quantity, proceeds_minor_units, disposed_at, source_entry_id FROM positions.disposal WHERE position_id = @positionId",
            connection))
        {
            command.Parameters.AddWithValue("positionId", positionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                disposals.Add(new Disposal(
                    reader.GetDecimal(0), Money.FromMinorUnits(reader.GetInt64(1), costCommodity),
                    reader.GetFieldValue<DateTimeOffset>(2), reader.GetGuid(3)));
            }
        }

        return Position.Reconstitute(
            new PositionId(positionId), tenantId, commodity, lots.ToImmutable(), disposals.ToImmutable(), quantity, costBasis);
    }

    public async Task ReplaceAsync(Position position, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // ON DELETE CASCADE takes lot/disposal rows with it — a sync always replaces the whole
        // projection for this tenant+commodity, never patches it incrementally (ADR-0018).
        await using (var delete = new NpgsqlCommand(
            "DELETE FROM positions.position WHERE tenant_id = @tenantId AND commodity = @commodity", connection, transaction))
        {
            delete.Parameters.AddWithValue("tenantId", position.TenantId.Value);
            delete.Parameters.AddWithValue("commodity", position.Commodity.Symbol);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO positions.position
                (position_id, tenant_id, commodity, cost_commodity, quantity, cost_basis_minor_units, synced_at)
            VALUES (@positionId, @tenantId, @commodity, @costCommodity, @quantity, @costBasisMinorUnits, @syncedAt)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("positionId", position.Id.Value);
            insert.Parameters.AddWithValue("tenantId", position.TenantId.Value);
            insert.Parameters.AddWithValue("commodity", position.Commodity.Symbol);
            insert.Parameters.AddWithValue("costCommodity", position.CostBasis.Commodity.Symbol);
            insert.Parameters.AddWithValue("quantity", position.Quantity);
            insert.Parameters.AddWithValue("costBasisMinorUnits", position.CostBasis.AmountMinorUnits);
            insert.Parameters.AddWithValue("syncedAt", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var lot in position.Lots)
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO positions.lot (lot_id, position_id, quantity, unit_cost_minor_units, acquired_at, source_entry_id)
                VALUES (@lotId, @positionId, @quantity, @unitCostMinorUnits, @acquiredAt, @sourceEntryId)
                """, connection, transaction);
            insert.Parameters.AddWithValue("lotId", lot.Id.Value);
            insert.Parameters.AddWithValue("positionId", position.Id.Value);
            insert.Parameters.AddWithValue("quantity", lot.Quantity);
            insert.Parameters.AddWithValue("unitCostMinorUnits", lot.UnitCost.AmountMinorUnits);
            insert.Parameters.AddWithValue("acquiredAt", lot.AcquiredAt);
            insert.Parameters.AddWithValue("sourceEntryId", lot.SourceEntryId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var disposal in position.Disposals)
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO positions.disposal (disposal_id, position_id, quantity, proceeds_minor_units, disposed_at, source_entry_id)
                VALUES (@disposalId, @positionId, @quantity, @proceedsMinorUnits, @disposedAt, @sourceEntryId)
                """, connection, transaction);
            insert.Parameters.AddWithValue("disposalId", Guid.NewGuid());
            insert.Parameters.AddWithValue("positionId", position.Id.Value);
            insert.Parameters.AddWithValue("quantity", disposal.Quantity);
            insert.Parameters.AddWithValue("proceedsMinorUnits", disposal.Proceeds.AmountMinorUnits);
            insert.Parameters.AddWithValue("disposedAt", disposal.DisposedAt);
            insert.Parameters.AddWithValue("sourceEntryId", disposal.SourceEntryId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
