using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Ingestion.Domain;

// Stage 5, PROPOSE (docs/03-architecture/05-ingestion-and-integration.md §3): a normalised,
// balanced pair of postings ready to hand to Ledger — Atlas.Kernel types only, never the row's raw
// strings (stage 3, NORMALISE, already happened by the time this exists). "EntryProposal ->
// PostJournalEntry. Never raw source rows" (docs/02-domain/03-context-map.md, R02).
public sealed record EntryProposal(
    string IdempotencyKey, ValidTime ValidTime, string Description, ImmutableArray<ProposedPosting> Postings);

public sealed record ProposedPosting(Guid AccountId, Money Money, string Direction); // "Debit" | "Credit"

public static class EntryProposalBuilder
{
    // Bank-statement sign convention: a positive amount (money arriving) debits the primary
    // account; a negative amount (money leaving) credits it. The counter-posting is always the
    // opposite direction against ColumnMapping.UnclassifiedAccountId.
    public static Result<EntryProposal> FromParsedRow(ParsedRow row, ColumnMapping mapping, string sourceId)
    {
        if (row.Amount == 0)
            return Result.Fail<EntryProposal>(IngestionDomainErrors.ZeroAmountRow);

        var commodity = Commodity.BySymbol(mapping.Commodity);
        var money = Money.FromDecimal(Math.Abs(row.Amount), commodity);
        var (primaryDirection, counterDirection) = row.Amount > 0 ? ("Debit", "Credit") : ("Credit", "Debit");

        var postings = ImmutableArray.Create(
            new ProposedPosting(mapping.PrimaryAccountId, money, primaryDirection),
            new ProposedPosting(mapping.UnclassifiedAccountId, money, counterDirection));

        var idempotencyKey = IdempotencyKey.Compute(sourceId, row.RawLine);
        return Result.Ok(new EntryProposal(idempotencyKey, new ValidTime(row.Date), row.Description, postings));
    }
}
