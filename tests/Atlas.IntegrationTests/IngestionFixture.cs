using Atlas.Modules.Ingestion.Infrastructure;
using Atlas.Modules.Ledger.Infrastructure;
using Atlas.Modules.Positions.Infrastructure;
using Azure.Storage.Blobs;
using Npgsql;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace Atlas.IntegrationTests;

// One Postgres + one Azurite container per test class. Ledger's, Ingestion's, and Positions'
// migrations all apply to the same Postgres instance (each in its own schema, per docs/03-
// architecture/03-modular-monolith.md §4) since importing a CSV posts into Ledger and syncing a
// Position reads it back out — the restricted data sources mirror what Atlas.Host wires up in
// production, not a test-only shortcut.
public sealed class IngestionFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly AzuriteContainer _azurite =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.34.0").Build();

    public NpgsqlDataSource LedgerDataSource { get; private set; } = null!;
    public NpgsqlDataSource IngestionDataSource { get; private set; } = null!;
    public NpgsqlDataSource PositionsDataSource { get; private set; } = null!;
    public BlobContainerClient BlobContainerClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());

        var superuserDataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());
        await using (var connection = await superuserDataSource.OpenConnectionAsync())
        {
            await LedgerMigrator.ApplyAsync(connection);
            await IngestionMigrator.ApplyAsync(connection);
            await PositionsMigrator.ApplyAsync(connection);
        }
        await superuserDataSource.DisposeAsync();

        var ledgerBuilder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Username = "atlas_ledger",
            Password = "atlas_ledger_dev_only",
        };
        LedgerDataSource = NpgsqlDataSource.Create(ledgerBuilder.ConnectionString);

        var ingestionBuilder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Username = "atlas_ingestion",
            Password = "atlas_ingestion_dev_only",
        };
        IngestionDataSource = NpgsqlDataSource.Create(ingestionBuilder.ConnectionString);

        var positionsBuilder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Username = "atlas_positions",
            Password = "atlas_positions_dev_only",
        };
        PositionsDataSource = NpgsqlDataSource.Create(positionsBuilder.ConnectionString);

        var blobServiceClient = new BlobServiceClient(_azurite.GetConnectionString(), AzuriteBlobClientOptions.Create());
        BlobContainerClient = blobServiceClient.GetBlobContainerClient("raw-payloads");
    }

    public async Task DisposeAsync()
    {
        await LedgerDataSource.DisposeAsync();
        await IngestionDataSource.DisposeAsync();
        await PositionsDataSource.DisposeAsync();
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IngestionCollection : ICollectionFixture<IngestionFixture>
{
    public const string Name = "Ingestion Postgres+Azurite";
}
