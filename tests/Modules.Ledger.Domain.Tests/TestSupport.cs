using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Modules.Ledger.Domain.Tests;

internal static class TestSupport
{
    public static readonly DateTimeOffset Day1 = new(2026, 1, 5, 18, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Day2 = new(2026, 1, 6, 18, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Day3 = new(2026, 1, 7, 18, 0, 0, TimeSpan.Zero);

    public static Posting Posting(AccountId accountId, long minorUnits, PostingDirection direction, Commodity? commodity = null) =>
        global::Atlas.Modules.Ledger.Domain.Entries.Posting
            .Create(accountId, Money.FromMinorUnits(minorUnits, commodity ?? Commodity.Brl), direction)
            .Value;

    public static JournalEntry BalancedEntry(
        AccountId debitAccount,
        AccountId creditAccount,
        long amountMinorUnits,
        DateTimeOffset validTime,
        DateTimeOffset decisionTime,
        string idempotencyKey,
        Commodity? commodity = null,
        string description = "test entry")
    {
        var postings = ImmutableArray.Create(
            Posting(debitAccount, amountMinorUnits, PostingDirection.Debit, commodity),
            Posting(creditAccount, amountMinorUnits, PostingDirection.Credit, commodity));

        return JournalEntry.Create(
                TenantId.New(),
                new ValidTime(validTime),
                new DecisionTime(decisionTime),
                currentTradingDayClose: validTime.AddDays(1),
                description,
                sourceId: "manual",
                idempotencyKey,
                postings)
            .Value;
    }
}
