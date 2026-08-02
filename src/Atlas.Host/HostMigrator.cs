using Npgsql;

namespace Atlas.Host;

// Bootstraps shared.schema_migrations (docs/03-architecture/03-modular-monolith.md §4: the "shared"
// schema is reserved for exactly this), takes an advisory lock so two starting instances can't race,
// and applies only migrations not already recorded — unlike LedgerMigrator (Slice 2's test helper),
// which reapplies unconditionally because Testcontainers always hands it a fresh database. This one
// runs against a persistent local volume, where "restart the host a second time" must not error.
public static class HostMigrator
{
    private const long AdvisoryLockKey = 891_001; // arbitrary, stable — see pg_advisory_lock docs

    public static async Task ApplyAsync(
        NpgsqlDataSource adminDataSource, IReadOnlyList<IAtlasModule> modules, CancellationToken cancellationToken = default)
    {
        await using var connection = await adminDataSource.OpenConnectionAsync(cancellationToken);

        await using (var bootstrap = connection.CreateCommand())
        {
            bootstrap.CommandText =
                """
                CREATE SCHEMA IF NOT EXISTS shared;
                CREATE TABLE IF NOT EXISTS shared.schema_migrations (
                    module          text NOT NULL,
                    migration_name  text NOT NULL,
                    applied_at      timestamptz NOT NULL DEFAULT now(),
                    PRIMARY KEY (module, migration_name)
                );
                """;
            await bootstrap.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var acquireLock = connection.CreateCommand())
        {
            acquireLock.CommandText = "SELECT pg_advisory_lock(@key)";
            acquireLock.Parameters.AddWithValue("key", AdvisoryLockKey);
            await acquireLock.ExecuteScalarAsync(cancellationToken);
        }

        try
        {
            foreach (var module in modules)
            {
                foreach (var migration in module.Migrations)
                {
                    if (await IsAppliedAsync(connection, module.Name, migration.Name, cancellationToken))
                        continue;

                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                    await using (var apply = new NpgsqlCommand(migration.Sql, connection, transaction))
                        await apply.ExecuteNonQueryAsync(cancellationToken);

                    await using (var record = new NpgsqlCommand(
                        "INSERT INTO shared.schema_migrations (module, migration_name) VALUES (@module, @name)",
                        connection, transaction))
                    {
                        record.Parameters.AddWithValue("module", module.Name);
                        record.Parameters.AddWithValue("name", migration.Name);
                        await record.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
            }
        }
        finally
        {
            await using var releaseLock = connection.CreateCommand();
            releaseLock.CommandText = "SELECT pg_advisory_unlock(@key)";
            releaseLock.Parameters.AddWithValue("key", AdvisoryLockKey);
            await releaseLock.ExecuteScalarAsync(cancellationToken);
        }
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection, string module, string migrationName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM shared.schema_migrations WHERE module = @module AND migration_name = @name";
        command.Parameters.AddWithValue("module", module);
        command.Parameters.AddWithValue("name", migrationName);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
