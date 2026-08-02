namespace Atlas.Modules.Ingestion.Domain;

// A candidate comparison, pre-computed from a Ledger.Contracts.JournalEntryPosted by the
// Application layer (Domain may reference only Atlas.Kernel — MR-1, and JournalEntryPosted lives in
// Ledger.Contracts).
public sealed record ExistingEntrySummary(Guid EntryId, DateTimeOffset ValidTime, string Description, long AbsoluteAmountMinorUnits);

public sealed record DuplicateCandidate(Guid ExistingEntryId, double Similarity);

// FR-110: "detect probable cross-source duplicates and queue for human resolution." Flags, never
// merges — docs/03-architecture/05-ingestion-and-integration.md §4: "Silent merging of financial
// records is a class of bug that is undetectable after the fact, so it is prohibited outright."
public static class DuplicateDetector
{
    private const int DateWindowDays = 2;
    private const double SimilarityThreshold = 0.85;

    public static IReadOnlyList<DuplicateCandidate> FindCandidates(
        DateTimeOffset proposalValidTime,
        string proposalDescription,
        long proposalAbsoluteAmountMinorUnits,
        Guid excludeEntryId,
        IReadOnlyList<ExistingEntrySummary> existingEntries)
    {
        var candidates = new List<DuplicateCandidate>();

        foreach (var existing in existingEntries)
        {
            if (existing.EntryId == excludeEntryId)
                continue; // never a candidate against itself

            if (existing.AbsoluteAmountMinorUnits != proposalAbsoluteAmountMinorUnits)
                continue; // "amount exact"

            if (Math.Abs((existing.ValidTime - proposalValidTime).TotalDays) > DateWindowDays)
                continue;

            var similarity = StringSimilarity.Compute(proposalDescription, existing.Description);
            if (similarity >= SimilarityThreshold)
                candidates.Add(new DuplicateCandidate(existing.EntryId, similarity));
        }

        return candidates;
    }
}
