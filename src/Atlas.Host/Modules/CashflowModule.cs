using Atlas.Modules.Cashflow.Application;
using Atlas.Modules.Cashflow.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Atlas.Host.Modules;

public sealed class CashflowModule : IAtlasModule
{
    private NpgsqlDataSource? _dataSource;

    public string Name => "Cashflow";
    public string Version => "0.1.0";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var adminConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var restrictedBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "atlas_cashflow",
            Password = "atlas_cashflow_dev_only", // local dev only — see Migrations/001_create_cashflow_schema.sql
        };

        _dataSource = NpgsqlDataSource.Create(restrictedBuilder.ConnectionString);

        // See LedgerModule.RegisterServices for why this is a factory closing over this module's own
        // instance rather than services.AddSingleton(_dataSource).
        var dataSource = _dataSource;
        services.AddScoped<IClassificationRepository>(_ => new ClassificationRepository(dataSource));
        // ClassifyCategoryHandler also depends on Ledger.Contracts.IFindAccount (MR-2), registered by
        // LedgerModule and resolved here automatically since both modules share one DI container.
        services.AddScoped<ClassifyCategoryHandler>();
    }

    public void RegisterEventHandlers(IEventBusBuilder eventBus)
    {
        // No subscribers — classification is an explicit, caller-driven decision (INV-060: never a
        // silent system assignment), not something triggered by a Ledger event.
    }

    public void RegisterEndpoints(IEndpointRouteBuilder endpoints) => CashflowEndpoints.Map(endpoints);

    public IReadOnlyList<Migration> Migrations { get; } =
        [.. CashflowMigrations.All.Select(m => new Migration(m.Name, m.Sql))];

    public IReadOnlyList<IHealthCheck> HealthChecks =>
        _dataSource is null
            ? throw new InvalidOperationException($"{nameof(RegisterServices)} must run before {nameof(HealthChecks)} is read.")
            : [new CashflowPostgresHealthCheck(_dataSource)];
}
