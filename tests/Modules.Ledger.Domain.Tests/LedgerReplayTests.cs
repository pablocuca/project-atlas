using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Modules.Ledger.Domain.Tests;

public class LedgerReplayTests
{
    private static readonly AccountId Checking = AccountId.New();
    private static readonly AccountId Salary = AccountId.New();

    [Fact]
    [BusinessRule("BR-107")]
    public void Replay_reconstructs_balance_from_multiple_entries_in_DecisionTime_order()
    {
        var entries = new[]
        {
            TestSupport.BalancedEntry(Checking, Salary, 10_000, TestSupport.Day1, TestSupport.Day1, "idem-a"),
            TestSupport.BalancedEntry(Checking, Salary, 5_000, TestSupport.Day2, TestSupport.Day2, "idem-b"),
        };

        var balance = LedgerReplay.BalanceAt(
            entries, Checking, Commodity.Brl, new ValidTime(TestSupport.Day3), new DecisionTime(TestSupport.Day3));

        Assert.Equal(15_000, balance.AmountMinorUnits);
    }

    [Fact]
    [BusinessRule("BR-102")]
    public void BalanceAt_excludes_entries_beyond_the_requested_time_coordinates()
    {
        var entries = new[]
        {
            TestSupport.BalancedEntry(Checking, Salary, 10_000, TestSupport.Day1, TestSupport.Day1, "idem-c"),
            TestSupport.BalancedEntry(Checking, Salary, 5_000, TestSupport.Day3, TestSupport.Day3, "idem-d"),
        };

        // asOf Day2: the Day3 entry is neither valid yet nor known yet.
        var balance = LedgerReplay.BalanceAt(
            entries, Checking, Commodity.Brl, new ValidTime(TestSupport.Day2), new DecisionTime(TestSupport.Day2));

        Assert.Equal(10_000, balance.AmountMinorUnits);
    }

    // This is the concrete proof behind the M0 exit-gate line: "a correction to a 3-week-old entry
    // preserves both the original belief and the corrected truth."
    [Fact]
    [BusinessRule("BR-101")]
    [BusinessRule("BR-107")]
    public void A_correction_preserves_both_the_original_belief_and_the_corrected_truth()
    {
        var original = TestSupport.BalancedEntry(Checking, Salary, 10_000, TestSupport.Day1, TestSupport.Day1, "idem-e");

        var correctedPostings = System.Collections.Immutable.ImmutableArray.Create(
            TestSupport.Posting(Checking, 12_000, PostingDirection.Debit),
            TestSupport.Posting(Salary, 12_000, PostingDirection.Credit));

        var (reversal, replacement) = original.Correct(new DecisionTime(TestSupport.Day3), "corrected", correctedPostings).Value;
        var allEntries = new[] { original, reversal, replacement };

        // Before the correction was known (asOfDecisionTime = Day2, before the Day3 correction):
        // BalanceAt's own time filter excludes the Day3 reversal/replacement, showing the original,
        // wrong belief — no manual filtering needed, which is the point of the bitemporal API.
        var beliefBeforeCorrection = LedgerReplay.BalanceAt(
            allEntries, Checking, Commodity.Brl, new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day2));

        // After the correction: querying at the same ValidTime, now, shows the corrected truth.
        var truthAfterCorrection = LedgerReplay.BalanceAt(
            allEntries, Checking, Commodity.Brl, new ValidTime(TestSupport.Day1), new DecisionTime(TestSupport.Day3));

        Assert.Equal(10_000, beliefBeforeCorrection.AmountMinorUnits);
        Assert.Equal(12_000, truthAfterCorrection.AmountMinorUnits);
    }
}
