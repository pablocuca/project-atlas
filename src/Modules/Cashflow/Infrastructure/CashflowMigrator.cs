using Npgsql;

namespace Atlas.Modules.Cashflow.Infrastructure;

// Mirrors Atlas.Modules.Ingestion.Infrastructure.IngestionMigrations/IngestionMigrator exactly —
// see that file's comments for the reasoning.
public static class CashflowMigrations
{
    public static IReadOnlyList<(string Name, string Sql)> All { get; } = Load();

    private static IReadOnlyList<(string, string)> Load()
    {
        var assembly = typeof(CashflowMigrations).Assembly;
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

public static class CashflowMigrator
{
    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        foreach (var (_, sql) in CashflowMigrations.All)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
