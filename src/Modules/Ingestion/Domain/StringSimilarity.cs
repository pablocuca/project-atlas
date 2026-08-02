namespace Atlas.Modules.Ingestion.Domain;

// Counterparty similarity for fuzzy duplicate detection (docs/03-architecture/
// 05-ingestion-and-integration.md §4: "counterparty similarity >= 0.85"). Substring-containment
// first, normalised Levenshtein as the fallback. Containment matters because it's the realistic
// case: a bank appends transaction metadata to a core name ("Joao" manually entered vs "JOAO S PIX
// RECEBIDO" from a feed), which raw edit distance scores as dissimilar even though a human reads it
// as an obvious match — checked against the architecture doc's own worked example
// ("Joao" vs "JOAO S"), which raw Levenshtein alone scores at 0.667, below the 0.85 threshold.
public static class StringSimilarity
{
    public static double Compute(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);

        if (normalizedA.Length == 0 && normalizedB.Length == 0)
            return 1.0;

        if (normalizedA.Length > 0 && normalizedB.Length > 0)
        {
            var (shorter, longer) = normalizedA.Length <= normalizedB.Length
                ? (normalizedA, normalizedB)
                : (normalizedB, normalizedA);

            if (longer.Contains(shorter, StringComparison.Ordinal))
                return 1.0;
        }

        var maxLength = Math.Max(normalizedA.Length, normalizedB.Length);
        if (maxLength == 0)
            return 1.0;

        var distance = LevenshteinDistance(normalizedA, normalizedB);
        return 1.0 - (double)distance / maxLength;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static int LevenshteinDistance(string a, string b)
    {
        var distances = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
            distances[i, 0] = i;
        for (var j = 0; j <= b.Length; j++)
            distances[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var substitutionCost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + substitutionCost);
            }
        }

        return distances[a.Length, b.Length];
    }
}
