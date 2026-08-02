using System.Collections.Immutable;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Atlas.Host.Modules;

public sealed class LedgerModule : IAtlasModule
{
    private NpgsqlDataSource? _dataSource;

    public string Name => "Ledger";
    public string Version => "0.1.0";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var adminConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        // Application code never connects as the migration-running admin user — it uses the
        // restricted role migration 001 creates, matching what Slice 2's integration tests prove
        // that role actually enforces (no UPDATE/DELETE beyond account.closed_at).
        var restrictedBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "atlas_ledger",
            Password = "atlas_ledger_dev_only", // local dev only — see Migrations/001_create_ledger_schema.sql
        };

        _dataSource = NpgsqlDataSource.Create(restrictedBuilder.ConnectionString);

        // Not services.AddSingleton(_dataSource): every module's data source is the same bare
        // NpgsqlDataSource type, so a second module's registration would silently shadow this one
        // in DI resolution (found the hard way — Ingestion's data source was resolving into
        // Ledger's AccountRepository, producing a "permission denied for schema ledger" error from
        // the wrong role entirely). Factories closing over this module's own _dataSource avoid the
        // type collision without needing keyed services for what's otherwise plain construction.
        var dataSource = _dataSource;
        services.AddScoped<IAccountRepository>(_ => new AccountRepository(dataSource));
        services.AddScoped<IJournalEntryRepository>(_ => new JournalEntryRepository(dataSource));
        services.AddScoped<OpenAccountHandler>();
        services.AddScoped<CloseAccountHandler>();
        services.AddScoped<PostJournalEntryHandler>();
        services.AddScoped<CorrectJournalEntryHandler>();
        services.AddScoped<BalanceAtHandler>();

        // The in-process port other modules use to post into the ledger (MR-2: they depend on
        // Ledger.Contracts only). First real consumer: Ingestion (M1 Slice 1).
        services.AddScoped<IPostJournalEntry, PostJournalEntryPort>();
    }

    public void RegisterEventHandlers(IEventBusBuilder eventBus)
    {
        // No subscribers yet — Ledger is still the only module. See IEventBusBuilder's doc comment.
    }

    public void RegisterEndpoints(IEndpointRouteBuilder endpoints) => LedgerEndpoints.Map(endpoints);

    public IReadOnlyList<Migration> Migrations { get; } =
        [.. LedgerMigrations.All.Select(m => new Migration(m.Name, m.Sql))];

    // Program.cs calls RegisterServices before reading this — the data source it needs doesn't
    // exist until then.
    public IReadOnlyList<IHealthCheck> HealthChecks =>
        _dataSource is null
            ? throw new InvalidOperationException($"{nameof(RegisterServices)} must run before {nameof(HealthChecks)} is read.")
            : [new LedgerPostgresHealthCheck(_dataSource)];
}
