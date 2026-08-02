using System.Text.Json;
using Atlas.Seed;

namespace Atlas.Seed.Tests;

public class SyntheticLifeGeneratorTests
{
    [Fact]
    public void Same_seed_and_years_produce_byte_identical_output()
    {
        var first = SyntheticLifeGenerator.Generate(seed: 42, years: 2);
        var second = SyntheticLifeGenerator.Generate(seed: 42, years: 2);

        // Records' generated equality is shallow on list-typed properties (reference equality for
        // the backing arrays) — a full JSON round-trip is the simplest true deep-equality check,
        // and it directly demonstrates "deterministic from a seed" (docs/03-architecture/
        // 09-devops-and-cicd.md §7) in a way a human can diff if it ever fails.
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Different_seeds_produce_different_output()
    {
        var first = SyntheticLifeGenerator.Generate(seed: 42, years: 2);
        var second = SyntheticLifeGenerator.Generate(seed: 43, years: 2);

        Assert.NotEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Ten_years_clears_the_M0_exit_gate_entry_count()
    {
        var life = SyntheticLifeGenerator.Generate(seed: 42, years: 10);

        // "1,000 synthetic entries" (docs/01-product/08-roadmap-and-milestones.md, M0 exit gate).
        Assert.True(life.Entries.Count >= 1000, $"Expected >= 1000 entries, got {life.Entries.Count}.");
    }

    [Fact]
    public void Every_generated_entry_balances_to_zero()
    {
        var life = SyntheticLifeGenerator.Generate(seed: 42, years: 2);

        foreach (var entry in life.Entries)
        {
            Assert.Equal(0, NetOf(entry.Postings));

            if (entry.Correction is { } correction)
                Assert.Equal(0, NetOf(correction.Postings));
        }
    }

    [Fact]
    public void Every_posting_references_a_known_account_code()
    {
        var life = SyntheticLifeGenerator.Generate(seed: 42, years: 2);
        var knownCodes = life.Accounts.Select(a => a.Code).ToHashSet();

        foreach (var entry in life.Entries)
        {
            Assert.All(entry.Postings, p => Assert.Contains(p.AccountCode, knownCodes));
            if (entry.Correction is { } correction)
                Assert.All(correction.Postings, p => Assert.Contains(p.AccountCode, knownCodes));
        }
    }

    private static long NetOf(IReadOnlyList<SyntheticPosting> postings) =>
        postings.Sum(p => p.Direction == "Debit" ? p.AmountMinorUnits : -p.AmountMinorUnits);
}
