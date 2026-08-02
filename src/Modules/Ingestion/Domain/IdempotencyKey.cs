using System.Security.Cryptography;
using System.Text;

namespace Atlas.Modules.Ingestion.Domain;

// idempotencyKey = SHA-256(sourceId concatenated with canonical(rawRecord))
// (docs/03-architecture/05-ingestion-and-integration.md §4) — computed from the raw, unparsed
// record, before any normalisation, "so that changes to Atlas's own parsing never alter the key of
// previously-imported data." This is what becomes JournalEntry.IdempotencyKey (BR-103) when the
// proposal is posted.
public static class IdempotencyKey
{
    // U+001F (Unit Separator) between the two fields — a control character never legitimately
    // present in a CSV line, so sourceId="ab"+"c" and sourceId="a"+"bc" can never collide.
    private const char FieldSeparator = '\u001F';

    public static string Compute(string sourceId, string rawRecord)
    {
        var input = $"{sourceId}{FieldSeparator}{rawRecord}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
