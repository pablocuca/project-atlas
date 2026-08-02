using Npgsql;

namespace Atlas.Modules.Ledger.Infrastructure;

// The embedded ledger/*.sql migrations, as (Name, Sql) pairs — the same data Atlas.Host's
// IAtlasModule.Migrations needs, and what LedgerMigrator (below) uses directly for tests.
public static class LedgerMigrations
{
    public static IReadOnlyList<(string Name, string Sql)> All { get; } = Load();

    private static IReadOnlyList<(string, string)> Load()
    {
        var assembly = typeof(LedgerMigrations).Assembly;
        const string marker = ".Migrations.";

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(marker, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        var migrations = new List<(string, string)>();
        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' could not be read.");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            var afterMarker = resourceName[(resourceName.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
            var name = afterMarker[..^".sql".Length];

            migrations.Add((name, sql));
        }

        return migrations;
    }
}

// A minimal, self-contained migration applier for this slice's own tests — applies every migration
// unconditionally, which is correct for Testcontainers' always-fresh database. Atlas.Host uses its
// own idempotent, tracking-aware migrator (HostMigrator) against a persistent local volume, sourced
// from the same LedgerMigrations.All data.
public static class LedgerMigrator
{
    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        foreach (var (_, sql) in LedgerMigrations.All)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
