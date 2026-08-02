using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Atlas.Modules.Ingestion.Domain;

// Stage 2, PARSE (docs/03-architecture/05-ingestion-and-integration.md §3): pure, no I/O — the raw
// payload is already captured by the time this runs. One bad row records a ParseFailure and the
// batch continues; nothing here can lose the rest of the file.
public static class CsvParser
{
    public static (ImmutableArray<ParsedRow> Rows, ImmutableArray<ParseFailure> Failures) Parse(
        RawPayload payload, ColumnMapping mapping)
    {
        var lines = payload.Content.Split('\n');
        var rows = ImmutableArray.CreateBuilder<ParsedRow>();
        var failures = ImmutableArray.CreateBuilder<ParseFailure>();
        var startIndex = mapping.HasHeaderRow ? 1 : 0;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var rowNumber = i + 1; // 1-based, matches the file line

            if (string.IsNullOrWhiteSpace(rawLine))
                continue; // a blank line is not a failure — nothing was there to fail

            var fields = SplitLine(rawLine);
            var maxIndex = Math.Max(mapping.DateColumnIndex, Math.Max(mapping.DescriptionColumnIndex, mapping.AmountColumnIndex));

            if (maxIndex >= fields.Length)
            {
                failures.Add(new ParseFailure(rowNumber, rawLine, $"Row has {fields.Length} column(s); mapping needs column {maxIndex}."));
                continue;
            }

            if (!DateTimeOffset.TryParse(
                fields[mapping.DateColumnIndex], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            {
                failures.Add(new ParseFailure(rowNumber, rawLine, $"Column {mapping.DateColumnIndex} is not a parseable date."));
                continue;
            }

            if (!decimal.TryParse(fields[mapping.AmountColumnIndex], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                failures.Add(new ParseFailure(rowNumber, rawLine, $"Column {mapping.AmountColumnIndex} is not a parseable amount."));
                continue;
            }

            rows.Add(new ParsedRow(rowNumber, rawLine, date, fields[mapping.DescriptionColumnIndex], amount));
        }

        return (rows.ToImmutable(), failures.ToImmutable());
    }

    // A minimal RFC4180-ish splitter — double-quoted fields, embedded commas, "" as an escaped
    // quote. Not the full spec; enough for real bank exports, which is what this needs to survive.
    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    current.Append(c);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
