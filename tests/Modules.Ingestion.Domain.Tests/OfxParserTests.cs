using Atlas.Modules.Ingestion.Domain;

namespace Modules.Ingestion.Domain.Tests;

public class OfxParserTests
{
    // OFX 1.x SGML "tag soup" — no closing tags on leaves. The real shape a bank export arrives in.
    private const string SgmlSample =
        """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102

        <OFX>
        <BANKMSGSRSV1>
        <STMTTRNRS>
        <STMTRS>
        <BANKTRANLIST>
        <STMTTRN>
        <TRNTYPE>CREDIT
        <DTPOSTED>20260105120000
        <TRNAMT>8000.00
        <FITID>2026010500001
        <NAME>Salary
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20260106
        <TRNAMT>-150.50
        <FITID>2026010600002
        <MEMO>Groceries
        </STMTTRN>
        </BANKTRANLIST>
        </STMTRS>
        </STMTTRNRS>
        </BANKMSGSRSV1>
        </OFX>
        """;

    [Fact]
    public void Parses_every_transaction_in_a_tag_soup_SGML_file()
    {
        var payload = new RawPayload(SgmlSample);

        var (rows, failures) = OfxParser.Parse(payload);

        Assert.Empty(failures);
        Assert.Equal(2, rows.Length);
        Assert.Equal("Salary", rows[0].Description);
        Assert.Equal(8000.00m, rows[0].Amount);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero), rows[0].Date);
        Assert.Equal("Groceries", rows[1].Description); // falls back to MEMO when NAME is absent
        Assert.Equal(-150.50m, rows[1].Amount);
        Assert.Equal(new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero), rows[1].Date); // date-only DTPOSTED
    }

    // BR-103's idempotency key hashes RawLine — for OFX, that must be the bank's own FITID, not the
    // full transaction block, so re-importing an overlapping statement window still dedups correctly
    // even if the exporter reformats whitespace between two exports.
    [Fact]
    public void RawLine_is_the_FITID_not_the_full_block()
    {
        var payload = new RawPayload(SgmlSample);

        var (rows, _) = OfxParser.Parse(payload);

        Assert.Equal("2026010500001", rows[0].RawLine);
        Assert.Equal("2026010600002", rows[1].RawLine);
    }

    // OFX 2.x is well-formed XML — closing tags present. The same extraction logic must handle both.
    [Fact]
    public void Parses_well_formed_XML_OFX_identically()
    {
        var xml =
            """
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20260107</DTPOSTED><TRNAMT>-2000.00</TRNAMT><FITID>3</FITID><NAME>Rent</NAME></STMTTRN>
            </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;
        var payload = new RawPayload(xml);

        var (rows, failures) = OfxParser.Parse(payload);

        Assert.Empty(failures);
        Assert.Single(rows);
        Assert.Equal("Rent", rows[0].Description);
        Assert.Equal(-2000.00m, rows[0].Amount);
    }

    [Fact]
    public void A_transaction_missing_DTPOSTED_is_recorded_as_a_failure_without_losing_the_batch()
    {
        var payload = new RawPayload(
            """
            <STMTTRN><TRNAMT>100.00</TRNAMT><FITID>1</FITID><NAME>Broken</NAME></STMTTRN>
            <STMTTRN><DTPOSTED>20260105</DTPOSTED><TRNAMT>50.00</TRNAMT><FITID>2</FITID><NAME>OK</NAME></STMTTRN>
            """);

        var (rows, failures) = OfxParser.Parse(payload);

        Assert.Single(rows);
        Assert.Equal("OK", rows[0].Description);
        Assert.Single(failures);
        Assert.Equal(1, failures[0].RowNumber);
    }

    [Fact]
    public void A_transaction_missing_TRNAMT_is_recorded_as_a_failure()
    {
        var payload = new RawPayload("<STMTTRN><DTPOSTED>20260105</DTPOSTED><FITID>1</FITID><NAME>Broken</NAME></STMTTRN>");

        var (rows, failures) = OfxParser.Parse(payload);

        Assert.Empty(rows);
        Assert.Single(failures);
    }

    [Fact]
    public void A_file_with_no_transactions_parses_to_an_empty_batch_not_an_error()
    {
        var payload = new RawPayload("<OFX><BANKMSGSRSV1></BANKMSGSRSV1></OFX>");

        var (rows, failures) = OfxParser.Parse(payload);

        Assert.Empty(rows);
        Assert.Empty(failures);
    }
}
