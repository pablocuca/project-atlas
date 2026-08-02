using Atlas.Kernel;
using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ingestion.Infrastructure;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Infrastructure;

namespace Atlas.IntegrationTests;

[Collection(IngestionCollection.Name)]
public class IngestionPipelineTests(IngestionFixture fixture)
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Importing_a_CSV_posts_valid_rows_and_records_a_parse_failure_without_losing_the_batch()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        var mapping = new ColumnMapping(checking, unclassified, "BRL", 0, 1, 2, HasHeaderRow: true);
        var handler = BuildHandler();

        const string csv =
            "date,description,amount\n" +
            "2026-01-05,Salary,8000.00\n" +
            "not-a-date,Broken row,50.00\n" +
            "2026-01-06,Groceries,-150.50\n";

        var result = await handler.HandleAsync(
            tenantId, "test-bank", new RawPayload(csv), mapping, new DecisionTime(Day1), Day1.AddDays(1), default);

        Assert.Equal(2, result.RowsParsed);
        Assert.Equal(2, result.EntriesPosted);
        Assert.Equal(0, result.DuplicatesSkipped);
        Assert.Single(result.ParseFailures);
        Assert.Contains("column", result.ParseFailures[0].Reason, StringComparison.OrdinalIgnoreCase);

        // The raw payload remains recoverable even though one row failed to parse
        // (docs/01-product/10-user-stories.md, US-010's malformed-row scenario).
        var archive = new BlobRawPayloadArchive(fixture.BlobContainerClient);
        var archived = await archive.DownloadAsync(result.BlobPath, default);
        Assert.Equal(csv, archived);
    }

    [Fact]
    public async Task Reimporting_an_overlapping_window_creates_zero_duplicates_and_does_not_double_count_balances()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        var mapping = new ColumnMapping(checking, unclassified, "BRL", 0, 1, 2, HasHeaderRow: true);
        var handler = BuildHandler();

        const string firstCsv =
            "date,description,amount\n" +
            "2026-01-05,Salary,8000.00\n" +
            "2026-01-06,Groceries,-150.50\n";

        var first = await handler.HandleAsync(
            tenantId, "test-bank", new RawPayload(firstCsv), mapping, new DecisionTime(Day1), Day1.AddDays(1), default);
        Assert.Equal(2, first.EntriesPosted);
        Assert.Equal(0, first.DuplicatesSkipped);

        // Overlapping window: the statement covers Groceries again (same raw line -> same
        // idempotency key, BR-103) plus a genuinely new row, Rent.
        const string secondCsv =
            "date,description,amount\n" +
            "2026-01-06,Groceries,-150.50\n" +
            "2026-01-07,Rent,-2000.00\n";

        var second = await handler.HandleAsync(
            tenantId, "test-bank", new RawPayload(secondCsv), mapping, new DecisionTime(Day1.AddDays(2)), Day1.AddDays(3), default);

        Assert.Equal(1, second.EntriesPosted);
        Assert.Equal(1, second.DuplicatesSkipped);

        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var checkingBalance = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(checking), Commodity.Brl,
            new ValidTime(Day1.AddDays(10)), new DecisionTime(Day1.AddDays(10)), default);

        // 8000.00 - 150.50 - 2000.00 = 5,849.50 -> 584,950 minor units. Proves Groceries was
        // recognised as a duplicate, not posted (and therefore not double-counted) a second time.
        Assert.Equal(584_950, checkingBalance.AmountMinorUnits);
    }

    private ImportCsvHandler BuildHandler()
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var postJournalEntry = new PostJournalEntryPort(new PostJournalEntryHandler(journalEntries));
        var archive = new BlobRawPayloadArchive(fixture.BlobContainerClient);
        var batches = new ImportBatchRepository(fixture.IngestionDataSource);

        return new ImportCsvHandler(archive, batches, postJournalEntry);
    }

    private async Task<(Guid Checking, Guid Unclassified)> OpenAccountsAsync(TenantId tenantId)
    {
        var openAccount = new OpenAccountHandler(new AccountRepository(fixture.LedgerDataSource));

        var checking = await openAccount.HandleAsync(
            tenantId, $"checking-{Guid.NewGuid():N}", "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);
        var unclassified = await openAccount.HandleAsync(
            tenantId, $"unclassified-{Guid.NewGuid():N}", "Unclassified", AccountType.Expense, Commodity.Brl, null, Day1, default);

        return (checking.Value.Id.Value, unclassified.Value.Id.Value);
    }
}
