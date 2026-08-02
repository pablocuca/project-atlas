using System.Text.Json.Serialization;
using Atlas.Host;
using Atlas.Host.Modules;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var adminConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var adminDataSource = NpgsqlDataSource.Create(adminConnectionString);

// Adding a module is meant to be this one line (docs/03-architecture/03-modular-monolith.md §5).
List<IAtlasModule> modules = [new LedgerModule()];

foreach (var module in modules)
    module.RegisterServices(builder.Services, builder.Configuration);

var healthChecks = builder.Services.AddHealthChecks();
foreach (var check in modules.SelectMany(m => m.HealthChecks))
    healthChecks.AddCheck(check.GetType().Name, check);

var app = builder.Build();

// Migrations apply before the app accepts traffic — a failed migration must abort startup, not
// serve requests against a schema it doesn't recognise.
await HostMigrator.ApplyAsync(adminDataSource, modules);

foreach (var module in modules)
    module.RegisterEndpoints(app);

app.MapHealthChecks("/health");

app.Run();
