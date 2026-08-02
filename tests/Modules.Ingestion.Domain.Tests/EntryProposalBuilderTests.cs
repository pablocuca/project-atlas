using Atlas.Modules.Ingestion.Domain;
using FsCheck;
using FsCheck.Xunit;

namespace Modules.Ingestion.Domain.Tests;

public class EntryProposalBuilderTests
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
    public void A_positive_amount_debits_the_primary_account_and_credits_the_counter_account()
    {
        var row = new ParsedRow(1, "raw", DateTimeOffset.UtcNow, "Salary", 8000.00m);

        var proposal = EntryProposalBuilder.FromParsedRow(row, Mapping, "bank").Value;

        var primary = Assert.Single(proposal.Postings, p => p.AccountId == Mapping.PrimaryAccountId);
        var counter = Assert.Single(proposal.Postings, p => p.AccountId == Mapping.UnclassifiedAccountId);
        Assert.Equal("Debit", primary.Direction);
        Assert.Equal("Credit", counter.Direction);
    }

    [Fact]
    public void A_negative_amount_credits_the_primary_account_and_debits_the_counter_account()
    {
        var row = new ParsedRow(1, "raw", DateTimeOffset.UtcNow, "Groceries", -150.50m);

        var proposal = EntryProposalBuilder.FromParsedRow(row, Mapping, "bank").Value;

        var primary = Assert.Single(proposal.Postings, p => p.AccountId == Mapping.PrimaryAccountId);
        var counter = Assert.Single(proposal.Postings, p => p.AccountId == Mapping.UnclassifiedAccountId);
        Assert.Equal("Credit", primary.Direction);
        Assert.Equal("Debit", counter.Direction);
    }

    [Fact]
    public void A_zero_amount_row_is_rejected()
    {
        var row = new ParsedRow(1, "raw", DateTimeOffset.UtcNow, "Nothing", 0m);

        var result = EntryProposalBuilder.FromParsedRow(row, Mapping, "bank");

        Assert.True(result.IsFailure);
        Assert.Equal("INGESTION.ZERO_AMOUNT_ROW", result.Error.Code);
    }

    // Testing Strategy's "entry balance" property, applied here: any generated non-zero amount
    // produces a proposal whose two postings balance to zero (BR-100, enforced again once posted).
    [Property]
    public bool Every_proposal_balances_to_zero(NonZeroInt amountCents)
    {
        var row = new ParsedRow(1, "raw", DateTimeOffset.UtcNow, "Row", amountCents.Get / 100m);

        var proposal = EntryProposalBuilder.FromParsedRow(row, Mapping, "bank").Value;

        var net = proposal.Postings.Sum(p => p.Direction == "Debit" ? p.Money.AmountMinorUnits : -p.Money.AmountMinorUnits);
        return net == 0;
    }
}
