using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Infrastructure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Atlas.Host.Modules;

public sealed class IngestionModule : IAtlasModule
{
    private NpgsqlDataSource? _dataSource;

    public string Name => "Ingestion";
    public string Version => "0.1.0";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var adminConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
        var blobConnectionString = configuration.GetConnectionString("AzuriteBlob")
            ?? throw new InvalidOperationException("ConnectionStrings:AzuriteBlob is not configured.");

        // Same pattern as LedgerModule: application code never connects as the migration-running
        // admin user — it uses the restricted role migration 001 creates.
        var restrictedBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "atlas_ingestion",
            Password = "atlas_ingestion_dev_only", // local dev only — see Migrations/001_create_ingestion_schema.sql
        };

        _dataSource = NpgsqlDataSource.Create(restrictedBuilder.ConnectionString);

        var blobServiceClient = new BlobServiceClient(blobConnectionString, AzuriteBlobClientOptions.Create());
        var blobContainerClient = blobServiceClient.GetBlobContainerClient("raw-payloads");

        // See LedgerModule.RegisterServices for why these are factories closing over this module's
        // own instances rather than services.AddSingleton(_dataSource) / AddSingleton(blobContainerClient)
        // — a second module registering the same bare type would silently shadow this one in DI.
        var dataSource = _dataSource;
        services.AddScoped<IRawPayloadArchive>(_ => new BlobRawPayloadArchive(blobContainerClient));
        services.AddScoped<IImportBatchRepository>(_ => new ImportBatchRepository(dataSource));
        services.AddScoped<ImportCsvHandler>();
    }

    public void RegisterEventHandlers(IEventBusBuilder eventBus)
    {
        // No subscribers yet — Ingestion doesn't publish domain events of its own this slice.
    }

    public void RegisterEndpoints(IEndpointRouteBuilder endpoints) => IngestionEndpoints.Map(endpoints);

    public IReadOnlyList<Migration> Migrations { get; } =
        [.. IngestionMigrations.All.Select(m => new Migration(m.Name, m.Sql))];

    public IReadOnlyList<IHealthCheck> HealthChecks =>
        _dataSource is null
            ? throw new InvalidOperationException($"{nameof(RegisterServices)} must run before {nameof(HealthChecks)} is read.")
            : [new IngestionPostgresHealthCheck(_dataSource)];
}
