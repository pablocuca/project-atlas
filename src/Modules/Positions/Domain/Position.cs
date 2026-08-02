using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Positions.Domain;

public readonly record struct PositionId(Guid Value)
{
    public static PositionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

// Position := aggregate holding of one Commodity across all Lots, at a point in time
// (docs/00-foundation/05-ubiquitous-language.md). A projection over the Ledger (ADR-0018) — fully
// rebuildable from JournalEntryPosted, never independently authored, so INV-040 ("position
// quantities reconcile to ledger quantities exactly") holds by construction rather than by a
// separate check: Quantity only ever changes via Acquire/Dispose calls driven by Ledger postings.
public sealed record Position
{
    public PositionId Id { get; }
    public TenantId TenantId { get; }
    public Commodity Commodity { get; }
    public ImmutableArray<Lot> Lots { get; }
    public ImmutableArray<Disposal> Disposals { get; }
    public decimal Quantity { get; }

    // The running total cost basis, in the position's cost commodity (BRL). AverageUnitCost is
    // derived, never stored independently, so it can never drift out of sync with Quantity/CostBasis.
    public Money CostBasis { get; }

    public Money AverageUnitCost => Quantity == 0
        ? Money.Zero(CostBasis.Commodity)
        : Money.FromDecimal(ToDecimal(CostBasis) / Quantity, CostBasis.Commodity);

    private Position(
        PositionId id, TenantId tenantId, Commodity commodity,
        ImmutableArray<Lot> lots, ImmutableArray<Disposal> disposals, decimal quantity, Money costBasis)
    {
        Id = id;
        TenantId = tenantId;
        Commodity = commodity;
        Lots = lots;
        Disposals = disposals;
        Quantity = quantity;
        CostBasis = costBasis;
    }

    public static Position Empty(TenantId tenantId, Commodity commodity, Commodity costCommodity) =>
        new(PositionId.New(), tenantId, commodity, [], [], 0m, Money.Zero(costCommodity));

    // INV-043: custo médio. The new average unit cost is (existing cost basis + acquired cost) /
    // (existing quantity + acquired quantity) — the Lot itself is retained unmodified for audit.
    public Result<Position> Acquire(decimal quantity, Money unitCost, DateTimeOffset acquiredAt, Guid sourceEntryId)
    {
        if (quantity <= 0)
            return Result.Fail<Position>(PositionDomainErrors.QuantityMustBePositive);
        if (unitCost.Commodity != CostBasis.Commodity)
            return Result.Fail<Position>(PositionDomainErrors.CostCommodityMismatch);

        var lot = new Lot(LotId.New(), quantity, unitCost, acquiredAt, sourceEntryId);
        var acquiredCost = Money.FromDecimal(ToDecimal(unitCost) * quantity, CostBasis.Commodity);

        return Result.Ok(new Position(
            Id, TenantId, Commodity, Lots.Add(lot), Disposals, Quantity + quantity, CostBasis.Add(acquiredCost)));
    }

    // INV-041: may not exceed the remaining quantity. INV-043: the basis removed is quantity times
    // the CURRENT weighted-average unit cost — never a specific Lot's cost, which is the whole point
    // of custo médio versus FIFO. Lots themselves are untouched; only the aggregate quantity/basis
    // and the Disposals audit trail change.
    public Result<Position> Dispose(decimal quantity, Money proceeds, DateTimeOffset disposedAt, Guid sourceEntryId)
    {
        if (quantity <= 0)
            return Result.Fail<Position>(PositionDomainErrors.QuantityMustBePositive);
        if (quantity > Quantity)
            return Result.Fail<Position>(PositionDomainErrors.InsufficientQuantity);
        if (proceeds.Commodity != CostBasis.Commodity)
            return Result.Fail<Position>(PositionDomainErrors.CostCommodityMismatch);

        var removedCost = Money.FromDecimal(ToDecimal(AverageUnitCost) * quantity, CostBasis.Commodity);
        var newQuantity = Quantity - quantity;
        // A fully-closed position carries no residual basis, however rounding drifted across a
        // sequence of acquisitions/disposals at half-even minor-unit precision.
        var newCostBasis = newQuantity == 0 ? Money.Zero(CostBasis.Commodity) : CostBasis.Subtract(removedCost);
        var disposal = new Disposal(quantity, proceeds, disposedAt, sourceEntryId);

        return Result.Ok(new Position(Id, TenantId, Commodity, Lots, Disposals.Add(disposal), newQuantity, newCostBasis));
    }

    // For Infrastructure to rehydrate a row it already knows is valid — see Account.Reconstitute for
    // the same reasoning.
    public static Position Reconstitute(
        PositionId id, TenantId tenantId, Commodity commodity,
        ImmutableArray<Lot> lots, ImmutableArray<Disposal> disposals, decimal quantity, Money costBasis) =>
        new(id, tenantId, commodity, lots, disposals, quantity, costBasis);

    private static decimal ToDecimal(Money money) => money.AmountMinorUnits / Pow10(money.Commodity.MinorUnitScale);

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
            result *= 10m;

        return result;
    }
}
