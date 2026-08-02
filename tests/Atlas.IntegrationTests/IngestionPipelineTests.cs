using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ingestion.Infrastructure;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;
using Atlas.Modules.Ledger.Infrastructure;
using Npgsql;

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

    // US-011 (docs/01-product/10-user-stories.md): a manual entry and a bank-fed entry for the
    // same real-world transfer, close in date, same amount, similar counterparty text — flagged as
    // a probable duplicate, but NEITHER record is merged or suppressed. Both remain posted.
    [Fact]
    public async Task A_cross_source_near_duplicate_is_flagged_but_both_entries_still_post()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);

        // Manual entry (docs/03-architecture/adr/ADR-0010: manual entry is Source #1, posted the
        // same way any other source is — directly through Ledger, never through Ingestion).
        var manualPostings = ImmutableArray.Create(
            Posting.Create(new AccountId(unclassified), Money.FromMinorUnits(120_000, Commodity.Brl), PostingDirection.Debit).Value,
            Posting.Create(new AccountId(checking), Money.FromMinorUnits(120_000, Commodity.Brl), PostingDirection.Credit).Value);

        var manualHandler = new PostJournalEntryHandler(journalEntries);
        var manualResult = await manualHandler.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "Joao", "manual", $"manual-{Guid.NewGuid()}", manualPostings, default);
        Assert.True(manualResult.IsSuccess);
        var manualEntryId = manualResult.Value.Entry.Id.Value;

        // A bank-fed row for the same transfer, one day later, same amount, a similar (bank-style)
        // description — a probable duplicate, not a proven one.
        var mapping = new ColumnMapping(checking, unclassified, "BRL", 0, 1, 2, HasHeaderRow: true);
        const string csv = "date,description,amount\n2026-01-06,JOAO S,-1200.00\n";
        var handler = BuildHandler();

        var importResult = await handler.HandleAsync(
            tenantId, "test-bank", new RawPayload(csv), mapping, new DecisionTime(Day1.AddDays(1)), Day1.AddDays(2), default);

        Assert.Equal(1, importResult.EntriesPosted); // never suppressed
        Assert.Equal(0, importResult.DuplicatesSkipped); // not a BR-103 exact duplicate — a different idempotency key
        Assert.Equal(1, importResult.DuplicateCandidatesFlagged);

        await using var connection = await fixture.IngestionDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT existing_entry_id, similarity, status FROM ingestion.duplicate_candidate WHERE tenant_id = @tenantId", connection);
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(manualEntryId, reader.GetGuid(0));
        Assert.True(reader.GetDouble(1) >= 0.85);
        Assert.Equal("Pending", reader.GetString(2));
        Assert.False(await reader.ReadAsync()); // exactly one candidate row
    }

    // US-012 (docs/01-product/10-user-stories.md): an exact-match reported balance reconciles.
    [Fact]
    public async Task Reconciliation_within_tolerance_is_marked_reconciled()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        await PostManualEntryAsync(tenantId, checking, unclassified, 8_000_00, Day1);

        var reconcile = BuildReconcileHandler();
        var reported = Money.FromMinorUnits(8_000_00, Commodity.Brl);
        var outcome = await reconcile.HandleAsync(
            tenantId, "test-bank", checking, reported, new ValidTime(Day1.AddDays(1)), new DecisionTime(Day1.AddDays(1)), default);

        Assert.True(outcome.IsReconciled);
        Assert.Equal(0, outcome.DiscrepancyMinorUnits);
    }

    // US-012's drift scenario: a discrepancy well outside tolerance is recorded as a breach —
    // BR-108 forbids a silent adjusting entry, so the account balance itself must be provably
    // unchanged by the act of reconciling.
    [Fact]
    public async Task A_discrepancy_over_tolerance_is_recorded_without_touching_the_ledger()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        await PostManualEntryAsync(tenantId, checking, unclassified, 1_429_000, Day1); // R$14.290,00

        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var balanceBefore = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(checking), Commodity.Brl, new ValidTime(Day1.AddDays(1)), new DecisionTime(Day1.AddDays(1)), default);

        var reconcile = BuildReconcileHandler();
        var reported = Money.FromMinorUnits(1_432_891, Commodity.Brl); // R$14.328,91 — R$38,91 off
        var outcome = await reconcile.HandleAsync(
            tenantId, "test-bank", checking, reported, new ValidTime(Day1.AddDays(1)), new DecisionTime(Day1.AddDays(1)), default);

        Assert.False(outcome.IsReconciled);
        Assert.Equal(3_891, outcome.DiscrepancyMinorUnits);

        var balanceAfter = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(checking), Commodity.Brl, new ValidTime(Day1.AddDays(1)), new DecisionTime(Day1.AddDays(1)), default);
        Assert.Equal(balanceBefore.AmountMinorUnits, balanceAfter.AmountMinorUnits); // no silent adjustment, BR-108

        await using var connection = await fixture.IngestionDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT is_reconciled, discrepancy_minor_units FROM ingestion.reconciliation WHERE tenant_id = @tenantId", connection);
        command.Parameters.AddWithValue("tenantId", tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.Equal(3_891L, reader.GetInt64(1));
    }

    private ImportCsvHandler BuildHandler()
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var postJournalEntry = new PostJournalEntryPort(new PostJournalEntryHandler(journalEntries));
        var findEntriesInRange = new FindEntriesInRangePort(journalEntries);
        var archive = new BlobRawPayloadArchive(fixture.BlobContainerClient);
        var batches = new ImportBatchRepository(fixture.IngestionDataSource);
        var duplicateCandidates = new DuplicateCandidateRepository(fixture.IngestionDataSource);

        return new ImportCsvHandler(archive, batches, duplicateCandidates, postJournalEntry, findEntriesInRange);
    }

    private ReconcileSourceHandler BuildReconcileHandler()
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var queryBalance = new QueryLedgerBalancePort(new BalanceAtHandler(journalEntries));
        var reconciliations = new ReconciliationRepository(fixture.IngestionDataSource);

        return new ReconcileSourceHandler(queryBalance, reconciliations);
    }

    private async Task PostManualEntryAsync(TenantId tenantId, Guid checking, Guid unclassified, long amountMinorUnits, DateTimeOffset validTime)
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var postings = ImmutableArray.Create(
            Posting.Create(new AccountId(checking), Money.FromMinorUnits(amountMinorUnits, Commodity.Brl), PostingDirection.Debit).Value,
            Posting.Create(new AccountId(unclassified), Money.FromMinorUnits(amountMinorUnits, Commodity.Brl), PostingDirection.Credit).Value);

        var handler = new PostJournalEntryHandler(journalEntries);
        var result = await handler.HandleAsync(
            tenantId, new ValidTime(validTime), new DecisionTime(validTime), validTime.AddDays(1),
            "opening balance", "manual", $"manual-{Guid.NewGuid()}", postings, default);
        Assert.True(result.IsSuccess);
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
