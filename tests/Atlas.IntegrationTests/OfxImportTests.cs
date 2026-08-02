using Atlas.Kernel;
using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ingestion.Infrastructure;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Infrastructure;

namespace Atlas.IntegrationTests;

// FR-108.
[Collection(IngestionCollection.Name)]
public class OfxImportTests(IngestionFixture fixture)
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    private const string Ofx =
        """
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260105120000<TRNAMT>8000.00<FITID>ofx-1<NAME>Salary
        </STMTTRN>
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260106<TRNAMT>-150.50<FITID>ofx-2<NAME>Groceries
        </STMTTRN>
        </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    [Fact]
    public async Task Importing_an_OFX_statement_posts_every_transaction_and_nets_to_the_expected_balance()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        var handler = BuildHandler();

        var result = await handler.HandleAsync(
            tenantId, "test-bank-ofx", new RawPayload(Ofx), checking, unclassified, "BRL",
            new DecisionTime(Day1), Day1.AddDays(1), default);

        Assert.Equal(2, result.RowsParsed);
        Assert.Equal(2, result.EntriesPosted);
        Assert.Empty(result.ParseFailures);

        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var balance = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(checking), Commodity.Brl, new ValidTime(Day1.AddDays(2)), new DecisionTime(Day1.AddDays(2)), default);

        Assert.Equal(784_950, balance.AmountMinorUnits); // 8000.00 - 150.50
    }

    // BR-103, FR-109: re-importing the same statement (same FITIDs) must not double-post.
    [Fact]
    public async Task Reimporting_the_same_OFX_statement_creates_zero_duplicates()
    {
        var tenantId = TenantId.New();
        var (checking, unclassified) = await OpenAccountsAsync(tenantId);
        var handler = BuildHandler();

        var first = await handler.HandleAsync(
            tenantId, "test-bank-ofx", new RawPayload(Ofx), checking, unclassified, "BRL",
            new DecisionTime(Day1), Day1.AddDays(1), default);
        var second = await handler.HandleAsync(
            tenantId, "test-bank-ofx", new RawPayload(Ofx), checking, unclassified, "BRL",
            new DecisionTime(Day1.AddDays(1)), Day1.AddDays(2), default);

        Assert.Equal(2, first.EntriesPosted);
        Assert.Equal(0, first.DuplicatesSkipped);
        Assert.Equal(0, second.EntriesPosted);
        Assert.Equal(2, second.DuplicatesSkipped);

        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var balance = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(checking), Commodity.Brl, new ValidTime(Day1.AddDays(3)), new DecisionTime(Day1.AddDays(3)), default);

        Assert.Equal(784_950, balance.AmountMinorUnits); // unchanged by the reimport
    }

    private ImportOfxHandler BuildHandler()
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var postJournalEntry = new PostJournalEntryPort(new PostJournalEntryHandler(journalEntries));
        var findEntriesInRange = new FindEntriesInRangePort(journalEntries);
        var archive = new BlobRawPayloadArchive(fixture.BlobContainerClient);
        var batches = new ImportBatchRepository(fixture.IngestionDataSource);
        var duplicateCandidates = new DuplicateCandidateRepository(fixture.IngestionDataSource);

        return new ImportOfxHandler(archive, batches, duplicateCandidates, postJournalEntry, findEntriesInRange);
    }

    private async Task<(Guid Checking, Guid Unclassified)> OpenAccountsAsync(TenantId tenantId)
    {
        var accounts = new AccountRepository(fixture.LedgerDataSource);
        var handler = new OpenAccountHandler(accounts);

        var checking = await handler.HandleAsync(
            tenantId, $"1.1.ofx-checking-{Guid.NewGuid()}", "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);
        var unclassified = await handler.HandleAsync(
            tenantId, $"5.9.ofx-unclassified-{Guid.NewGuid()}", "Unclassified", AccountType.Expense, Commodity.Brl, null, Day1, default);

        return (checking.Value.Id.Value, unclassified.Value.Id.Value);
    }
}
