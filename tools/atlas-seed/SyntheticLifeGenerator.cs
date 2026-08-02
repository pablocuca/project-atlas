using System.Security.Cryptography;
using System.Text;

namespace Atlas.Seed;

// Deterministic (docs/03-architecture/09-devops-and-cicd.md §7: "deterministic from a seed") —
// same (seed, years, asOf) always produces byte-identical output. Never reads DateTime.Now: dates
// are computed relative to asOf, which defaults to a fixed constant, not "today" — otherwise the
// documented `--years 10 --seed 42` with no --as-of would silently differ every day it's run.
public static class SyntheticLifeGenerator
{
    // The whole synthetic life is single-commodity — realistic for a Brazilian persona and enough
    // to exercise the bitemporal machinery without adding a second dimension to the dataset.
    public const string Commodity = "BRL";

    private static readonly DateTimeOffset DefaultAsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ~2% of entries later get a correction — enough to exercise belief-vs-truth queries at scale
    // without dominating the dataset.
    private const double CorrectionRate = 0.02;

    public static SyntheticLife Generate(int seed, int years, DateTimeOffset? asOf = null)
    {
        var effectiveAsOf = asOf ?? DefaultAsOf;
        var random = new Random(seed);
        var tenantId = DeterministicGuid($"tenant:{seed}");

        var accounts = BuildAccounts(effectiveAsOf, years);
        var checking = accounts[0];
        var salaryAccount = accounts.Single(a => a.Code == "4.1.salary");
        var expenseAccounts = accounts.Where(a => a.Type == "Expense").ToArray();

        var startDate = effectiveAsOf.AddYears(-years);
        var entries = new List<SyntheticEntry>();
        var sequence = 0;

        for (var monthOffset = 0; monthOffset < years * 12; monthOffset++)
        {
            var monthStart = startDate.AddMonths(monthOffset);

            var salaryDay = new DateTimeOffset(monthStart.Year, monthStart.Month, 5, 12, 0, 0, TimeSpan.Zero);
            var salaryAmount = 800_000 + random.Next(-50_000, 50_001); // R$8,000 +/- R$500, minor units
            entries.Add(CreateEntry(
                tenantId, ref sequence, salaryDay, "salary",
                [
                    new SyntheticPosting(checking.Code, salaryAmount, "Debit"),
                    new SyntheticPosting(salaryAccount.Code, salaryAmount, "Credit"),
                ]));

            // Guaranteed >= 8/month regardless of draws, so --years 10 clears 1,000 entries even
            // in the worst case: 120 months * (1 salary + 8 min expenses) = 1,080.
            var expenseCount = 8 + random.Next(0, 4);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            for (var i = 0; i < expenseCount; i++)
            {
                var day = 1 + random.Next(0, daysInMonth);
                var expenseDate = new DateTimeOffset(monthStart.Year, monthStart.Month, day, 12, 0, 0, TimeSpan.Zero);
                var expenseAccount = expenseAccounts[random.Next(expenseAccounts.Length)];
                var amount = 5_000 + random.Next(0, 60_000); // R$50 - R$650

                entries.Add(CreateEntry(
                    tenantId, ref sequence, expenseDate, $"expense:{expenseAccount.Name}",
                    [
                        new SyntheticPosting(expenseAccount.Code, amount, "Debit"),
                        new SyntheticPosting(checking.Code, amount, "Credit"),
                    ]));
            }
        }

        entries = ApplyCorrections(entries, random);

        return new SyntheticLife(tenantId, accounts, entries);
    }

    private static IReadOnlyList<SyntheticAccount> BuildAccounts(DateTimeOffset asOf, int years)
    {
        var openedAt = asOf.AddYears(-years);

        (string Code, string Name, string Type)[] definitions =
        [
            ("1.1.checking", "Checking", "Asset"),
            ("1.2.savings", "Savings", "Asset"),
            ("4.1.salary", "Salary", "Income"),
            ("5.1.housing", "Housing", "Expense"),
            ("5.2.food", "Food", "Expense"),
            ("5.3.transport", "Transport", "Expense"),
            ("5.4.leisure", "Leisure", "Expense"),
            ("5.5.other", "Other", "Expense"),
        ];

        return definitions
            .Select(d => new SyntheticAccount(d.Code, d.Name, d.Type, openedAt))
            .ToArray();
    }

    private static SyntheticEntry CreateEntry(
        Guid tenantId, ref int sequence, DateTimeOffset validTime, string description, SyntheticPosting[] postings)
    {
        sequence++;
        var idempotencyKey = $"seed-{tenantId:N}-{sequence:D6}";
        return new SyntheticEntry(idempotencyKey, validTime, validTime, description, postings, Correction: null);
    }

    private static List<SyntheticEntry> ApplyCorrections(List<SyntheticEntry> entries, Random random)
    {
        var result = new List<SyntheticEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (random.NextDouble() >= CorrectionRate)
            {
                result.Add(entry);
                continue;
            }

            var correctionDecisionTime = entry.DecisionTime.AddDays(1 + random.Next(0, 90));
            var delta = 1_000 + random.Next(0, 20_000);
            var correctedPostings = entry.Postings
                .Select(p => p with { AmountMinorUnits = p.AmountMinorUnits + delta })
                .ToArray();

            result.Add(entry with
            {
                Correction = new SyntheticCorrection($"{entry.Description} (corrected)", correctionDecisionTime, correctedPostings),
            });
        }

        return result;
    }

    // Stable per input string, not cryptographic — just needs to be the same every time.
    private static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash[..16]);
    }
}
