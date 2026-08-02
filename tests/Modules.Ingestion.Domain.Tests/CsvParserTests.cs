using Atlas.Modules.Ingestion.Domain;

namespace Modules.Ingestion.Domain.Tests;

public class CsvParserTests
{
    private static readonly ColumnMapping Mapping = new(
        PrimaryAccountId: Guid.NewGuid(),
        UnclassifiedAccountId: Guid.NewGuid(),
        Commodity: "BRL",
        DateColumnIndex: 0,
        DescriptionColumnIndex: 1,
        AmountColumnIndex: 2,
        HasHeaderRow: true);

    [Fact]
    public void Parses_every_well_formed_row()
    {
        var payload = new RawPayload(
            "date,description,amount\n" +
            "2026-01-05,Salary,8000.00\n" +
            "2026-01-06,Groceries,-150.50\n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Empty(failures);
        Assert.Equal(2, rows.Length);
        Assert.Equal("Salary", rows[0].Description);
        Assert.Equal(8000.00m, rows[0].Amount);
        Assert.Equal(-150.50m, rows[1].Amount);
    }

    [Fact]
    public void A_malformed_row_is_recorded_as_a_failure_without_losing_the_rest_of_the_batch()
    {
        var payload = new RawPayload(
            "date,description,amount\n" +
            "2026-01-05,Salary,8000.00\n" +
            "not-a-date,Broken row,100.00\n" +
            "2026-01-07,Rent,-2000.00\n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Equal(2, rows.Length);
        Assert.Single(failures);
        Assert.Equal(3, failures[0].RowNumber);
        Assert.Contains("column", failures[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_unparseable_amount_is_recorded_as_a_failure()
    {
        var payload = new RawPayload("date,description,amount\n2026-01-05,Salary,not-a-number\n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Empty(rows);
        Assert.Single(failures);
    }

    [Fact]
    public void A_row_with_too_few_columns_is_recorded_as_a_failure()
    {
        var payload = new RawPayload("date,description,amount\n2026-01-05,Salary\n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Empty(rows);
        Assert.Single(failures);
    }

    [Fact]
    public void Blank_lines_are_skipped_without_becoming_failures()
    {
        var payload = new RawPayload("date,description,amount\n2026-01-05,Salary,8000.00\n\n   \n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Single(rows);
        Assert.Empty(failures);
    }

    [Fact]
    public void Quoted_fields_with_embedded_commas_are_parsed_correctly()
    {
        var payload = new RawPayload("date,description,amount\n2026-01-05,\"Restaurant, Sao Paulo\",-89.90\n");

        var (rows, failures) = CsvParser.Parse(payload, Mapping);

        Assert.Empty(failures);
        Assert.Equal("Restaurant, Sao Paulo", rows[0].Description);
    }

    [Fact]
    public void Header_row_is_skipped_when_HasHeaderRow_is_true()
    {
        var payload = new RawPayload("date,description,amount\n2026-01-05,Salary,8000.00\n");

        var (rows, _) = CsvParser.Parse(payload, Mapping);

        Assert.Single(rows);
    }

    [Fact]
    public void No_header_row_is_parsed_as_data_when_HasHeaderRow_is_false()
    {
        var mappingNoHeader = Mapping with { HasHeaderRow = false };
        var payload = new RawPayload("2026-01-05,Salary,8000.00\n");

        var (rows, failures) = CsvParser.Parse(payload, mappingNoHeader);

        Assert.Empty(failures);
        Assert.Single(rows);
    }
}
