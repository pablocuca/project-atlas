namespace Atlas.Seed;

public sealed record VerificationResult(int ChecksPerformed, IReadOnlyList<string> Mismatches)
{
    public bool Success => Mismatches.Count == 0;
}

// The actual proof behind the M0 exit-gate line "1,000 synthetic entries post, balance, and query
// correctly at arbitrary bitemporal coordinates" — an independent, in-memory replay of exactly what
// was posted, checked against what the live API returns. Not a smoke test; a real oracle.
public sealed class Verifier(AtlasHostClient client)
{
    public async Task<VerificationResult> VerifyAsync(
        Guid tenantId,
        SyntheticLife life,
        IReadOnlyDictionary<string, Guid> accountIdsByCode,
        int randomSampleSize,
        Random random,
        CancellationToken cancellationToken)
    {
        var mismatches = new List<string>();
        var checkedCount = 0;

        foreach (var (accountCode, asOfValidTime, asOfDecisionTime) in BuildSampleCoordinates(life, randomSampleSize, random))
        {
            var expected = ComputeExpectedBalance(life, accountCode, asOfValidTime, asOfDecisionTime);
            var accountId = accountIdsByCode[accountCode];
            var actual = await client.GetBalanceAsync(tenantId, accountId, asOfValidTime, asOfDecisionTime, cancellationToken);

            checkedCount++;
            if (actual != expected)
                mismatches.Add($"{accountCode} asOf(V={asOfValidTime:O}, D={asOfDecisionTime:O}): expected {expected}, got {actual}");
        }

        return new VerificationResult(checkedCount, mismatches);
    }

    // Pure, in-memory replay — debit-positive, matching LedgerReplay.BalanceAt's convention exactly
    // (src/Modules/Ledger/Domain/Entries/LedgerReplay.cs).
    private static long ComputeExpectedBalance(
        SyntheticLife life, string accountCode, DateTimeOffset asOfValidTime, DateTimeOffset asOfDecisionTime)
    {
        long balance = 0;

        foreach (var entry in life.Entries)
        {
            Apply(entry.Postings, entry.ValidTime, entry.DecisionTime, accountCode, asOfValidTime, asOfDecisionTime, ref balance);

            if (entry.Correction is { } correction)
            {
                // Decision 0001: the reversal exactly negates the original; the replacement is the
                // corrected postings — both at the original ValidTime, the correction's DecisionTime.
                Apply(Negate(entry.Postings), entry.ValidTime, correction.DecisionTime, accountCode, asOfValidTime, asOfDecisionTime, ref balance);
                Apply(correction.Postings, entry.ValidTime, correction.DecisionTime, accountCode, asOfValidTime, asOfDecisionTime, ref balance);
            }
        }

        return balance;
    }

    private static void Apply(
        IReadOnlyList<SyntheticPosting> postings,
        DateTimeOffset validTime,
        DateTimeOffset decisionTime,
        string accountCode,
        DateTimeOffset asOfValidTime,
        DateTimeOffset asOfDecisionTime,
        ref long balance)
    {
        if (validTime > asOfValidTime || decisionTime > asOfDecisionTime)
            return;

        foreach (var posting in postings.Where(p => p.AccountCode == accountCode))
            balance += posting.Direction == "Debit" ? posting.AmountMinorUnits : -posting.AmountMinorUnits;
    }

    private static IReadOnlyList<SyntheticPosting> Negate(IReadOnlyList<SyntheticPosting> postings) =>
        [.. postings.Select(p => p with { Direction = p.Direction == "Debit" ? "Credit" : "Debit" })];

    private static List<(string AccountCode, DateTimeOffset AsOfValidTime, DateTimeOffset AsOfDecisionTime)> BuildSampleCoordinates(
        SyntheticLife life, int randomSampleSize, Random random)
    {
        var coordinates = new List<(string, DateTimeOffset, DateTimeOffset)>();

        // Always include, for every corrected entry, the coordinate just before the correction
        // (recovers the original wrong belief) and just after (recovers the corrected truth) — the
        // exact scenario Decision 0001 exists to make possible, exercised at scale, not just once.
        foreach (var entry in life.Entries.Where(e => e.Correction is not null))
        {
            var correctionTime = entry.Correction!.DecisionTime;
            foreach (var posting in entry.Postings)
            {
                coordinates.Add((posting.AccountCode, entry.ValidTime, correctionTime.AddSeconds(-1)));
                coordinates.Add((posting.AccountCode, entry.ValidTime, correctionTime.AddSeconds(1)));
            }
        }

        var accountCodes = life.Accounts.Select(a => a.Code).ToArray();
        var latestDecisionTime = life.Entries
            .SelectMany(e => new[] { e.DecisionTime, e.Correction?.DecisionTime ?? e.DecisionTime })
            .Max();

        for (var i = 0; i < randomSampleSize; i++)
        {
            var accountCode = accountCodes[random.Next(accountCodes.Length)];
            var entry = life.Entries[random.Next(life.Entries.Count)];
            coordinates.Add((accountCode, entry.ValidTime, latestDecisionTime));
        }

        return coordinates;
    }
}
