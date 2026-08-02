using Npgsql;

namespace Atlas.Modules.Ledger.Infrastructure;

// A minimal, self-contained migration applier for this slice — reads the embedded ledger/*.sql
// files and runs them in order. A dependency-ordered, per-module runner across all modules
// (Atlas.Host.Migrator, per docs/05-engineering/01-repository-structure.md) arrives in Slice 3 when
// there's more than one module's migrations to sequence; this is what Slice 2's own tests need today.
public static class LedgerMigrator
{
    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        var assembly = typeof(LedgerMigrator).Assembly;
        var migrationResourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var resourceName in migrationResourceNames)
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' could not be read.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
