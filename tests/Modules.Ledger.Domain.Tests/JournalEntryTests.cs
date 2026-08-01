using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;
using FsCheck;
using FsCheck.Xunit;

namespace Modules.Ledger.Domain.Tests;

public class JournalEntryTests
{
    private static readonly AccountId Checking = AccountId.New();
    private static readonly AccountId Salary = AccountId.New();

    [Fact]
    [BusinessRule("BR-100")]
    public void Create_fails_when_postings_do_not_balance()
    {
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 900, PostingDirection.Credit));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day1),
            TestSupport.Day1.AddDays(1), "unbalanced", "manual", "idem-1", postings);

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ENTRY_UNBALANCED", result.Error.Code);
    }

    [Fact]
    [BusinessRule("BR-100")]
    public void Create_succeeds_with_independent_multi_commodity_balance()
    {
        // INV-030: multi-commodity entries are permitted; each commodity balances independently.
        var trading = AccountId.New();
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 5000, PostingDirection.Credit, Commodity.Brl),
            TestSupport.Posting(trading, 5000, PostingDirection.Debit, Commodity.Brl),
            TestSupport.Posting(trading, 1000, PostingDirection.Credit, Commodity.Usd),
            TestSupport.Posting(Salary, 1000, PostingDirection.Debit, Commodity.Usd));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day1),
            TestSupport.Day1.AddDays(1), "fx trade", "manual", "idem-2", postings);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [BusinessRule("BR-101")]
    public void Correct_produces_two_new_entries_and_never_mutates_the_original()
    {
        var original = TestSupport.BalancedEntry(Checking, Salary, 1000, TestSupport.Day1, TestSupport.Day1, "idem-3");

        var correctedPostings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1200, PostingDirection.Debit),
            TestSupport.Posting(Salary, 1200, PostingDirection.Credit));

        var result = original.Correct(new DecisionTime(TestSupport.Day2), "corrected amount", correctedPostings);

        Assert.True(result.IsSuccess);
        var (reversal, replacement) = result.Value;

        Assert.Equal(original.Id, reversal.CorrectsEntryId);
        Assert.Equal(original.Id, replacement.CorrectsEntryId);
        Assert.Equal(JournalEntryKind.Reversal, reversal.Kind);
        Assert.Equal(JournalEntryKind.Replacement, replacement.Kind);
        Assert.NotEqual(reversal.Id, replacement.Id);

        // The original is untouched: still Original kind, still its own postings, still no CorrectsEntryId.
        Assert.Equal(JournalEntryKind.Original, original.Kind);
        Assert.Null(original.CorrectsEntryId);
        Assert.Equal(1000, original.Postings[0].Money.AmountMinorUnits);

        // The reversal exactly negates the original in signed terms.
        var originalNet = original.Postings.Sum(p => p.SignedMinorUnits());
        var reversalNet = reversal.Postings.Sum(p => p.SignedMinorUnits());
        Assert.Equal(-originalNet, reversalNet);
    }

    [Fact]
    [BusinessRule("BR-101")]
    public void Correct_restates_at_the_original_ValidTime_not_the_correction_date()
    {
        var original = TestSupport.BalancedEntry(Checking, Salary, 1000, TestSupport.Day1, TestSupport.Day1, "idem-4");
        var correctedPostings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 1000, PostingDirection.Credit));

        var (reversal, replacement) = original.Correct(new DecisionTime(TestSupport.Day3), "same amount, different note", correctedPostings).Value;

        Assert.Equal(original.ValidTime, reversal.ValidTime);
        Assert.Equal(original.ValidTime, replacement.ValidTime);
        Assert.Equal(new DecisionTime(TestSupport.Day3), reversal.DecisionTime);
        Assert.Equal(new DecisionTime(TestSupport.Day3), replacement.DecisionTime);
    }

    [Fact]
    [BusinessRule("BR-102")]
    public void Every_entry_carries_both_ValidTime_and_DecisionTime_as_supplied()
    {
        var entry = TestSupport.BalancedEntry(Checking, Salary, 500, TestSupport.Day1, TestSupport.Day2, "idem-5");

        Assert.Equal(new ValidTime(TestSupport.Day1), entry.ValidTime);
        Assert.Equal(new DecisionTime(TestSupport.Day2), entry.DecisionTime);
    }

    [Fact]
    [BusinessRule("BR-103")]
    public void Create_fails_when_idempotency_key_is_blank()
    {
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 1000, PostingDirection.Credit));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day1),
            TestSupport.Day1.AddDays(1), "no key", "manual", "  ", postings);

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ENTRY_IDEMPOTENCY_KEY_REQUIRED", result.Error.Code);
    }

    [Fact]
    [BusinessRule("BR-104")]
    public void Create_fails_when_ValidTime_exceeds_the_current_trading_day_close()
    {
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 1000, PostingDirection.Credit));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day2), new DecisionTime(TestSupport.Day1),
            currentTradingDayClose: TestSupport.Day1, "future dated", "manual", "idem-6", postings);

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ENTRY_VALID_TIME_IN_FUTURE", result.Error.Code);
    }

    [Fact]
    [BusinessRule("BR-109")]
    public void Manual_entry_goes_through_the_same_validation_as_any_other_source()
    {
        // There is exactly one Create factory; "manual" is an ordinary sourceId string, not a
        // privileged path, so an unbalanced manual entry is rejected exactly like any other.
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, 1000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 999, PostingDirection.Credit));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day1),
            TestSupport.Day1.AddDays(1), "manual unbalanced", "manual", "idem-7", postings);

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ENTRY_UNBALANCED", result.Error.Code);
    }

    // Testing Strategy §2: "Entry balance — any generated valid entry sums to zero per commodity."
    [Property]
    [BusinessRule("BR-100")]
    public bool Any_valid_entry_balances_to_zero_per_commodity(PositiveInt amount)
    {
        var postings = ImmutableArray.Create(
            TestSupport.Posting(Checking, amount.Get, PostingDirection.Debit),
            TestSupport.Posting(Salary, amount.Get, PostingDirection.Credit));

        var result = JournalEntry.Create(
            TenantId.New(), new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day1),
            TestSupport.Day1.AddDays(1), "property", "manual", $"idem-prop-{amount.Get}", postings);

        var net = result.Value.Postings.GroupBy(p => p.Money.Commodity).Select(g => g.Sum(p => p.SignedMinorUnits()));
        return result.IsSuccess && net.All(n => n == 0);
    }
}
