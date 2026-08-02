using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;
using Atlas.Modules.Ledger.Infrastructure;
using Npgsql;

namespace Atlas.IntegrationTests;

[Collection(LedgerCollection.Name)]
public class LedgerPersistenceTests(LedgerFixture fixture)
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 1, 6, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day3 = new(2026, 1, 7, 18, 0, 0, TimeSpan.Zero);

    private OpenAccountHandler OpenAccount => new(new AccountRepository(fixture.RestrictedDataSource));
    private CloseAccountHandler CloseAccount => new(
        new AccountRepository(fixture.RestrictedDataSource),
        new JournalEntryRepository(fixture.RestrictedDataSource));
    private PostJournalEntryHandler PostEntry => new(new JournalEntryRepository(fixture.RestrictedDataSource));
    private CorrectJournalEntryHandler CorrectEntry => new(new JournalEntryRepository(fixture.RestrictedDataSource));
    private BalanceAtHandler BalanceAt => new(new JournalEntryRepository(fixture.RestrictedDataSource));

    [Fact]
    public async Task Opening_an_account_round_trips_through_Postgres()
    {
        var tenantId = TenantId.New();
        var opened = await OpenAccount.HandleAsync(
            tenantId, $"1.1.{Guid.NewGuid():N}"[..12], "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);

        Assert.True(opened.IsSuccess);

        var reloaded = await new AccountRepository(fixture.RestrictedDataSource)
            .FindByIdAsync(tenantId, opened.Value.Id, default);

        Assert.NotNull(reloaded);
        Assert.Equal(opened.Value.Code, reloaded.Code);
        Assert.Equal(AccountType.Asset, reloaded.Type);
        Assert.Equal(Commodity.Brl, reloaded.Commodity);
        Assert.Null(reloaded.ClosedAt);
    }

    [Fact]
    public async Task Duplicate_account_code_for_the_same_tenant_is_rejected_by_the_database()
    {
        var tenantId = TenantId.New();
        var code = $"dup-{Guid.NewGuid():N}"[..16];

        var first = await OpenAccount.HandleAsync(tenantId, code, "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);
        Assert.True(first.IsSuccess);

        var second = await OpenAccount.HandleAsync(tenantId, code, "Also Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);

        Assert.True(second.IsFailure);
        Assert.Equal("LEDGER.ACCOUNT_CODE_ALREADY_IN_USE", second.Error.Code);
    }

    [Fact]
    public async Task Posting_and_querying_a_journal_entry_round_trips_through_Postgres()
    {
        var tenantId = TenantId.New();
        var (checking, salary) = await OpenTwoAccountsAsync(tenantId);

        var posted = await PostEntry.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "salary", "manual", $"idem-{Guid.NewGuid()}", BalancedPostings(checking, salary, 10_000), default);

        Assert.True(posted.IsSuccess);

        var balance = await BalanceAt.HandleAsync(
            tenantId, checking, Commodity.Brl, new ValidTime(Day2), new DecisionTime(Day2), default);

        Assert.Equal(10_000, balance.AmountMinorUnits);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_is_rejected_by_the_database()
    {
        var tenantId = TenantId.New();
        var (checking, salary) = await OpenTwoAccountsAsync(tenantId);
        var idempotencyKey = $"idem-{Guid.NewGuid()}";

        var first = await PostEntry.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "salary", "manual", idempotencyKey, BalancedPostings(checking, salary, 10_000), default);
        Assert.True(first.IsSuccess);

        var second = await PostEntry.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "salary again", "manual", idempotencyKey, BalancedPostings(checking, salary, 10_000), default);

        Assert.True(second.IsFailure);
        Assert.Equal("LEDGER.DUPLICATE_IDEMPOTENCY_KEY", second.Error.Code);
    }

    // The concrete, Postgres-backed proof of the M0 exit-gate line: "a correction to a 3-week-old
    // entry preserves both the original belief and the corrected truth" — same scenario as
    // Modules.Ledger.Domain.Tests' in-memory version, now against a real database.
    [Fact]
    public async Task A_correction_preserves_both_the_original_belief_and_the_corrected_truth_in_Postgres()
    {
        var tenantId = TenantId.New();
        var (checking, salary) = await OpenTwoAccountsAsync(tenantId);

        var posted = await PostEntry.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "salary", "manual", $"idem-{Guid.NewGuid()}", BalancedPostings(checking, salary, 10_000), default);

        var corrected = await CorrectEntry.HandleAsync(
            tenantId, posted.Value.Entry.Id, new DecisionTime(Day3), "corrected amount",
            BalancedPostings(checking, salary, 12_000), default);
        Assert.True(corrected.IsSuccess);

        var beliefBeforeCorrection = await BalanceAt.HandleAsync(
            tenantId, checking, Commodity.Brl, new ValidTime(Day1), new DecisionTime(Day2), default);
        var truthAfterCorrection = await BalanceAt.HandleAsync(
            tenantId, checking, Commodity.Brl, new ValidTime(Day1), new DecisionTime(Day3), default);

        Assert.Equal(10_000, beliefBeforeCorrection.AmountMinorUnits);
        Assert.Equal(12_000, truthAfterCorrection.AmountMinorUnits);
    }

    [Fact]
    public async Task Closing_an_account_updates_only_closed_at()
    {
        var tenantId = TenantId.New();
        var opened = await OpenAccount.HandleAsync(
            tenantId, $"close-{Guid.NewGuid():N}"[..16], "Empty", AccountType.Asset, Commodity.Brl, null, Day1, default);

        var closed = await CloseAccount.HandleAsync(tenantId, opened.Value.Id, Day2, default);

        Assert.True(closed.IsSuccess);
        Assert.Equal(Day2, closed.Value.ClosedAt);
    }

    // NFR-705 / Decision 0002: the append-only guarantee is a database permission, not a promise the
    // application code makes — proved here by trying to break it as the actual restricted role.
    [Fact]
    public async Task The_atlas_ledger_role_cannot_update_or_delete_journal_entries_or_postings()
    {
        var tenantId = TenantId.New();
        var (checking, salary) = await OpenTwoAccountsAsync(tenantId);
        var posted = await PostEntry.HandleAsync(
            tenantId, new ValidTime(Day1), new DecisionTime(Day1), Day1.AddDays(1),
            "salary", "manual", $"idem-{Guid.NewGuid()}", BalancedPostings(checking, salary, 10_000), default);

        await AssertInsufficientPrivilegeAsync(
            $"UPDATE ledger.journal_entry SET description = 'tampered' WHERE entry_id = '{posted.Value.Entry.Id.Value}'");
        await AssertInsufficientPrivilegeAsync(
            $"DELETE FROM ledger.journal_entry WHERE entry_id = '{posted.Value.Entry.Id.Value}'");
        await AssertInsufficientPrivilegeAsync("UPDATE ledger.posting SET minor_units = 0");
        await AssertInsufficientPrivilegeAsync("DELETE FROM ledger.posting");
    }

    [Fact]
    public async Task The_atlas_ledger_role_cannot_update_account_fields_other_than_closed_at()
    {
        var tenantId = TenantId.New();
        var opened = await OpenAccount.HandleAsync(
            tenantId, $"immut-{Guid.NewGuid():N}"[..16], "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);

        await AssertInsufficientPrivilegeAsync(
            $"UPDATE ledger.account SET type = 'Liability' WHERE account_id = '{opened.Value.Id.Value}'");
    }

    private async Task AssertInsufficientPrivilegeAsync(string sql)
    {
        await using var command = fixture.RestrictedDataSource.CreateCommand(sql);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private async Task<(AccountId Checking, AccountId Salary)> OpenTwoAccountsAsync(TenantId tenantId)
    {
        var checking = await OpenAccount.HandleAsync(
            tenantId, $"checking-{Guid.NewGuid():N}"[..20], "Checking", AccountType.Asset, Commodity.Brl, null, Day1, default);
        var salary = await OpenAccount.HandleAsync(
            tenantId, $"salary-{Guid.NewGuid():N}"[..20], "Salary", AccountType.Income, Commodity.Brl, null, Day1, default);

        return (checking.Value.Id, salary.Value.Id);
    }

    private static ImmutableArray<Posting> BalancedPostings(AccountId debitAccount, AccountId creditAccount, long amount) =>
        ImmutableArray.Create(
            Posting.Create(debitAccount, Money.FromMinorUnits(amount, Commodity.Brl), PostingDirection.Debit).Value,
            Posting.Create(creditAccount, Money.FromMinorUnits(amount, Commodity.Brl), PostingDirection.Credit).Value);
}
