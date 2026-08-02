using Atlas.Modules.Positions.Application;
using Atlas.Modules.Positions.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Atlas.Host.Modules;

public sealed class PositionsModule : IAtlasModule
{
    private NpgsqlDataSource? _dataSource;

    public string Name => "Positions";
    public string Version => "0.1.0";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var adminConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var restrictedBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "atlas_positions",
            Password = "atlas_positions_dev_only", // local dev only — see Migrations/001_create_positions_schema.sql
        };

        _dataSource = NpgsqlDataSource.Create(restrictedBuilder.ConnectionString);

        // See LedgerModule.RegisterServices for why this is a factory closing over this module's own
        // instance rather than services.AddSingleton(_dataSource).
        var dataSource = _dataSource;
        services.AddScoped<IPositionRepository>(_ => new PositionRepository(dataSource));
        // SyncPositionHandler also depends on Ledger.Contracts.IFindEntriesInRange (MR-2), registered
        // by LedgerModule and resolved here automatically since both modules share one DI container.
        services.AddScoped<SyncPositionHandler>();
    }

    public void RegisterEventHandlers(IEventBusBuilder eventBus)
    {
        // No subscribers — SyncPositionHandler pulls from Ledger via IFindEntriesInRange on demand
        // rather than subscribing to JournalEntryPosted (Decision 0010; no dispatcher exists yet).
    }

    public void RegisterEndpoints(IEndpointRouteBuilder endpoints) => PositionsEndpoints.Map(endpoints);

    public IReadOnlyList<Migration> Migrations { get; } =
        [.. PositionsMigrations.All.Select(m => new Migration(m.Name, m.Sql))];

    public IReadOnlyList<IHealthCheck> HealthChecks =>
        _dataSource is null
            ? throw new InvalidOperationException($"{nameof(RegisterServices)} must run before {nameof(HealthChecks)} is read.")
            : [new PositionsPostgresHealthCheck(_dataSource)];
}
