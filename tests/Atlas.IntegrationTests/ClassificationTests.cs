using Atlas.Kernel;
using Atlas.Modules.Cashflow.Application;
using Atlas.Modules.Cashflow.Domain;
using Atlas.Modules.Cashflow.Infrastructure;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Infrastructure;

namespace Atlas.IntegrationTests;

// FR-301, INV-060, US-013.
[Collection(IngestionCollection.Name)]
public class ClassificationTests(IngestionFixture fixture)
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reclassifying_a_category_preserves_the_original_decision_in_the_audit_trail()
    {
        var tenantId = TenantId.New();
        var accounts = new AccountRepository(fixture.LedgerDataSource);
        var opened = await new OpenAccountHandler(accounts).HandleAsync(
            tenantId, $"5.1.saude-{Guid.NewGuid()}", "Plano de saúde", AccountType.Expense, Commodity.Brl, null, Day1, default);
        Assert.True(opened.IsSuccess);
        var categoryAccountId = opened.Value.Id.Value;

        var handler = BuildHandler();

        var first = await handler.HandleAsync(
            tenantId, categoryAccountId, Classification.Discretionary, "initial guess", Day1, default);
        Assert.True(first.IsSuccess);

        var reclassified = await handler.HandleAsync(
            tenantId, categoryAccountId, Classification.Essential, "it's a health plan premium", Day1.AddDays(30), default);
        Assert.True(reclassified.IsSuccess);

        var repository = new ClassificationRepository(fixture.CashflowDataSource);
        var history = await repository.FindHistoryAsync(tenantId, categoryAccountId, default);

        Assert.Equal(2, history.Count); // INV-060: nothing was overwritten
        Assert.Equal(Classification.Essential, history.Current()!.Classification);
        Assert.Contains(history, d => d.Classification == Classification.Discretionary && d.Rationale == "initial guess");
    }

    // A category is exactly a Ledger Expense-type account this milestone (Decision 0011) — any
    // other account type is rejected rather than silently accepted.
    [Fact]
    public async Task Classifying_a_non_expense_account_fails()
    {
        var tenantId = TenantId.New();
        var accounts = new AccountRepository(fixture.LedgerDataSource);
        var opened = await new OpenAccountHandler(accounts).HandleAsync(
            tenantId, $"1.1.checking-{Guid.NewGuid()}", "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);
        Assert.True(opened.IsSuccess);

        var handler = BuildHandler();
        var result = await handler.HandleAsync(
            tenantId, opened.Value.Id.Value, Classification.Discretionary, null, Day1, default);

        Assert.True(result.IsFailure);
        Assert.Equal("CASHFLOW.CATEGORY_ACCOUNT_NOT_AN_EXPENSE", result.Error.Code);
    }

    [Fact]
    public async Task Classifying_a_nonexistent_account_fails()
    {
        var handler = BuildHandler();
        var result = await handler.HandleAsync(
            TenantId.New(), Guid.NewGuid(), Classification.Discretionary, null, Day1, default);

        Assert.True(result.IsFailure);
        Assert.Equal("CASHFLOW.CATEGORY_ACCOUNT_NOT_FOUND", result.Error.Code);
    }

    private ClassifyCategoryHandler BuildHandler()
    {
        var accounts = new AccountRepository(fixture.LedgerDataSource);
        var findAccount = new FindAccountPort(accounts);
        var decisions = new ClassificationRepository(fixture.CashflowDataSource);
        return new ClassifyCategoryHandler(findAccount, decisions);
    }
}
