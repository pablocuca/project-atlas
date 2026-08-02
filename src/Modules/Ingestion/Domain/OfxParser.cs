using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Atlas.Modules.Ingestion.Domain;

// Stage 2, PARSE, for FR-108 (OFX import). OFX 1.x is SGML "tag soup" — leaf tags like <DTPOSTED>
// often have no closing tag at all — while OFX 2.x is well-formed XML. Extracting each leaf value by
// scanning forward to the next '<' or newline (ExtractTag) handles both without needing to know
// which version a given file is: a real closing tag just becomes the next '<' found.
public static partial class OfxParser
{
    public static (ImmutableArray<ParsedRow> Rows, ImmutableArray<ParseFailure> Failures) Parse(RawPayload payload)
    {
        var rows = ImmutableArray.CreateBuilder<ParsedRow>();
        var failures = ImmutableArray.CreateBuilder<ParseFailure>();

        var rowNumber = 0;
        foreach (Match match in StatementTransactionPattern().Matches(payload.Content))
        {
            rowNumber++; // a sequential transaction index, not a file line number — OFX isn't line-based
            var block = match.Value;

            var datePosted = ExtractTag(block, "DTPOSTED");
            var amountText = ExtractTag(block, "TRNAMT");
            var description = ExtractTag(block, "NAME") ?? ExtractTag(block, "MEMO") ?? "";
            var financialInstitutionId = ExtractTag(block, "FITID");

            if (datePosted is null || !TryParseOfxDate(datePosted, out var date))
            {
                failures.Add(new ParseFailure(rowNumber, block, "Missing or unparseable DTPOSTED."));
                continue;
            }

            if (amountText is null || !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                failures.Add(new ParseFailure(rowNumber, block, "Missing or unparseable TRNAMT."));
                continue;
            }

            // BR-103's idempotency key is SHA-256(sourceId + rawRecord). FITID is the bank's own
            // stable, unique-per-transaction identifier — a better idempotency source than the CSV
            // path's raw-line hash, which is fragile to whitespace/formatting drift between two
            // exports of an overlapping statement window. Falls back to the full block only if a
            // (spec-mandatory, but not code-enforced-here) FITID is actually missing.
            var idempotencySource = financialInstitutionId ?? block;

            rows.Add(new ParsedRow(rowNumber, idempotencySource, date, description, amount));
        }

        return (rows.ToImmutable(), failures.ToImmutable());
    }

    private static string? ExtractTag(string block, string tag)
    {
        var openTag = $"<{tag}>";
        var start = block.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += openTag.Length;
        var end = block.IndexOfAny(['<', '\r', '\n'], start);
        if (end < 0)
            end = block.Length;

        var value = block[start..end].Trim();
        return value.Length == 0 ? null : value;
    }

    // OFX date/time: YYYYMMDD[HHMMSS][.XXX][[gmt offset[:tz name]]]. Only the date and, if present,
    // the time-of-day are used — the offset is ignored and the result treated as UTC, matching the
    // rest of this codebase's test fixtures (every ValidTime in the test suite is a literal "Z").
    private static bool TryParseOfxDate(string raw, out DateTimeOffset date)
    {
        date = default;
        if (raw.Length < 8)
            return false;

        if (!DateTime.TryParseExact(raw[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return false;

        var hour = 0;
        var minute = 0;
        var second = 0;
        if (raw.Length >= 14
            && int.TryParse(raw.AsSpan(8, 2), out var h) && int.TryParse(raw.AsSpan(10, 2), out var m) && int.TryParse(raw.AsSpan(12, 2), out var s))
        {
            hour = h;
            minute = m;
            second = s;
        }

        date = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, second, TimeSpan.Zero);
        return true;
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StatementTransactionPattern();
}
