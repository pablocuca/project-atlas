using Atlas.Modules.Ledger.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Atlas.IntegrationTests;

// One Postgres container per test class, migration applied once, superuser data source (for setup
// and negative-permission tests) plus a restricted data source connecting as atlas_ledger (what
// application code actually uses). IAsyncLifetime per xUnit's async setup/teardown convention.
public sealed class LedgerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public NpgsqlDataSource SuperuserDataSource { get; private set; } = null!;
    public NpgsqlDataSource RestrictedDataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        SuperuserDataSource = NpgsqlDataSource.Create(_container.GetConnectionString());

        await using (var connection = await SuperuserDataSource.OpenConnectionAsync())
        {
            await LedgerMigrator.ApplyAsync(connection);
        }

        var restrictedBuilder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = "atlas_ledger",
            Password = "atlas_ledger_dev_only",
        };
        RestrictedDataSource = NpgsqlDataSource.Create(restrictedBuilder.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await SuperuserDataSource.DisposeAsync();
        await RestrictedDataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class LedgerCollection : ICollectionFixture<LedgerFixture>
{
    public const string Name = "Ledger Postgres";
}
