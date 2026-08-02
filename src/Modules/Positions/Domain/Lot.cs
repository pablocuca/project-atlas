using Atlas.Kernel;

namespace Atlas.Modules.Positions.Domain;

public readonly record struct LotId(Guid Value)
{
    public static LotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

// Lot := a specific acquisition of a Commodity: quantity, unit cost, acquisition date
// (docs/00-foundation/05-ubiquitous-language.md). INV-043: for Brazilian equities the reportable
// cost basis is the weighted average across all of a Position's Lots, not any single Lot's own
// unit cost — Lots are retained individually anyway, for audit and for regimes that require lot
// identification (e.g. Exterior).
public sealed record Lot(LotId Id, decimal Quantity, Money UnitCost, DateTimeOffset AcquiredAt, Guid SourceEntryId);
