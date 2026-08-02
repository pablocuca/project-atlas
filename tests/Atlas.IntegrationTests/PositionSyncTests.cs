using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;
using Atlas.Modules.Ledger.Infrastructure;
using Atlas.Modules.Positions.Application;
using Atlas.Modules.Positions.Infrastructure;

namespace Atlas.IntegrationTests;

// FR-201, FR-202, INV-040, INV-041, INV-043. A trade is posted through Ledger like any other entry
// (Decision 0010) — BR-100 requires every commodity's postings to net to zero, so a buy/sell entry
// carries a clearing leg in each commodity alongside the real position/cash legs (INV-030).
[Collection(IngestionCollection.Name)]
public class PositionSyncTests(IngestionFixture fixture)
{
    private static readonly Commodity Petr4 = Commodity.Create("TEST.PETR4.INTEGRATION", CommodityKind.Equity, 0);
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    static PositionSyncTests() => Commodity.Register(Petr4);

    // US-012-style worked example for FR-202: buy 100 @ R$30,00, buy 50 more @ R$36,00 -> weighted
    // average R$32,00; sell 60 -> 90 remain, still averaging R$32,00, matching PositionTests'
    // domain-level example now proven end-to-end against real Postgres.
    [Fact]
    public async Task Syncing_replays_ledger_trades_into_a_weighted_average_position()
    {
        var tenantId = TenantId.New();
        var accounts = await OpenTradingAccountsAsync(tenantId);

        await PostTradeAsync(tenantId, accounts, 100m, 30.00m, isBuy: true, Day1);
        await PostTradeAsync(tenantId, accounts, 50m, 36.00m, isBuy: true, Day1.AddDays(1));
        await PostTradeAsync(tenantId, accounts, 60m, 35.00m, isBuy: false, Day1.AddDays(2));

        var handler = BuildSyncHandler();
        var position = await handler.HandleAsync(
            tenantId, accounts.PositionAccountId, accounts.CashAccountId, Petr4, Commodity.Brl,
            new ValidTime(Day1.AddDays(3)), new DecisionTime(Day1.AddDays(3)), default);

        Assert.Equal(90m, position.Quantity);
        Assert.Equal(288_000, position.CostBasis.AmountMinorUnits); // R$2.880,00
        Assert.Equal(3_200, position.AverageUnitCost.AmountMinorUnits); // R$32,00
        Assert.Equal(2, position.Lots.Length);
        Assert.Single(position.Disposals);

        // INV-040: the synced projection's quantity is exactly the ledger's own quantity for that
        // account — proven independently via IJournalEntryRepository.BalanceAtAsync, not just
        // trusted because SyncPositionHandler says so.
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var ledgerQuantity = await journalEntries.BalanceAtAsync(
            tenantId, new AccountId(accounts.PositionAccountId), Petr4,
            new ValidTime(Day1.AddDays(3)), new DecisionTime(Day1.AddDays(3)), default);
        Assert.Equal(ledgerQuantity.AmountMinorUnits, (long)position.Quantity);

        // And the persisted projection round-trips through PositionRepository correctly.
        var repository = new PositionRepository(fixture.PositionsDataSource);
        var persisted = await repository.FindAsync(tenantId, Petr4, default);
        Assert.NotNull(persisted);
        Assert.Equal(position.Quantity, persisted.Quantity);
        Assert.Equal(position.CostBasis, persisted.CostBasis);
    }

    private SyncPositionHandler BuildSyncHandler()
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var findEntriesInRange = new FindEntriesInRangePort(journalEntries);
        var positions = new PositionRepository(fixture.PositionsDataSource);
        return new SyncPositionHandler(findEntriesInRange, positions);
    }

    private async Task<TradingAccounts> OpenTradingAccountsAsync(TenantId tenantId)
    {
        var accounts = new AccountRepository(fixture.LedgerDataSource);
        var handler = new OpenAccountHandler(accounts);

        var position = await handler.HandleAsync(
            tenantId, $"1.2.petr4-{Guid.NewGuid()}", "PETR4 Holdings", AccountType.Asset, Petr4, null, Day1, default);
        var petr4Clearing = await handler.HandleAsync(
            tenantId, $"1.9.petr4-clearing-{Guid.NewGuid()}", "PETR4 Trading Clearing", AccountType.Asset, Petr4, null, Day1, default);
        var cash = await handler.HandleAsync(
            tenantId, $"1.1.cash-{Guid.NewGuid()}", "Brokerage Cash", AccountType.Asset, Commodity.Brl, null, Day1, default);
        var brlClearing = await handler.HandleAsync(
            tenantId, $"1.9.brl-clearing-{Guid.NewGuid()}", "BRL Trading Clearing", AccountType.Asset, Commodity.Brl, null, Day1, default);

        return new TradingAccounts(
            position.Value.Id.Value, petr4Clearing.Value.Id.Value, cash.Value.Id.Value, brlClearing.Value.Id.Value);
    }

    private async Task PostTradeAsync(
        TenantId tenantId, TradingAccounts accounts, decimal quantity, decimal unitPrice, bool isBuy, DateTimeOffset validTime)
    {
        var journalEntries = new JournalEntryRepository(fixture.LedgerDataSource);
        var handler = new PostJournalEntryHandler(journalEntries);

        var shareQuantity = Money.FromMinorUnits((long)quantity, Petr4);
        var cashAmount = Money.FromDecimal(quantity * unitPrice, Commodity.Brl);

        var (positionDirection, clearingCommodityDirection, clearingCashDirection, cashDirection) = isBuy
            ? (PostingDirection.Debit, PostingDirection.Credit, PostingDirection.Debit, PostingDirection.Credit)
            : (PostingDirection.Credit, PostingDirection.Debit, PostingDirection.Credit, PostingDirection.Debit);

        var postings = ImmutableArray.Create(
            Posting.Create(new AccountId(accounts.PositionAccountId), shareQuantity, positionDirection).Value,
            Posting.Create(new AccountId(accounts.Petr4ClearingAccountId), shareQuantity, clearingCommodityDirection).Value,
            Posting.Create(new AccountId(accounts.BrlClearingAccountId), cashAmount, clearingCashDirection).Value,
            Posting.Create(new AccountId(accounts.CashAccountId), cashAmount, cashDirection).Value);

        var result = await handler.HandleAsync(
            tenantId, new ValidTime(validTime), new DecisionTime(validTime), validTime.AddDays(1),
            isBuy ? "Buy PETR4" : "Sell PETR4", "manual", $"trade-{Guid.NewGuid()}", postings, default);

        Assert.True(result.IsSuccess);
    }

    private sealed record TradingAccounts(Guid PositionAccountId, Guid Petr4ClearingAccountId, Guid CashAccountId, Guid BrlClearingAccountId);
}
