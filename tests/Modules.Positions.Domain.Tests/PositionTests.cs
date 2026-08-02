using Atlas.Kernel;
using Atlas.Modules.Positions.Domain;
using FsCheck.Xunit;

namespace Modules.Positions.Domain.Tests;

public class PositionTests
{
    private static readonly Commodity Petr4 = Commodity.Create("TEST.PETR4", CommodityKind.Equity, 0);
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    // FR-202, INV-043: buy 100 @ R$30,00, buy 50 more @ R$36,00 -> weighted-average unit cost is
    // R$32,00, not either individual purchase price, and not a FIFO-first-lot R$30,00.
    [Fact]
    public void Acquiring_two_lots_computes_the_weighted_average_unit_cost()
    {
        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl);

        var afterFirst = position.Acquire(100m, Money.FromDecimal(30.00m, Commodity.Brl), Day1, Guid.NewGuid());
        Assert.True(afterFirst.IsSuccess);

        var afterSecond = afterFirst.Value.Acquire(50m, Money.FromDecimal(36.00m, Commodity.Brl), Day1.AddDays(1), Guid.NewGuid());
        Assert.True(afterSecond.IsSuccess);

        var position2 = afterSecond.Value;
        Assert.Equal(150m, position2.Quantity);
        Assert.Equal(480_000, position2.CostBasis.AmountMinorUnits); // R$4.800,00
        Assert.Equal(3_200, position2.AverageUnitCost.AmountMinorUnits); // R$32,00
        Assert.Equal(2, position2.Lots.Length); // INV-043: lots retained individually for audit
    }

    // INV-043: a disposal removes basis at the CURRENT weighted-average cost, never a specific lot's
    // cost — selling 60 of the 150-share position above removes 60 * R$32,00 = R$1.920,00, leaving
    // 90 shares still averaging R$32,00 (2.880,00 / 90).
    [Fact]
    public void Disposing_removes_basis_at_the_current_weighted_average_not_a_specific_lot()
    {
        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl)
            .Acquire(100m, Money.FromDecimal(30.00m, Commodity.Brl), Day1, Guid.NewGuid()).Value
            .Acquire(50m, Money.FromDecimal(36.00m, Commodity.Brl), Day1.AddDays(1), Guid.NewGuid()).Value;

        var afterDisposal = position.Dispose(60m, Money.FromDecimal(35.00m * 60, Commodity.Brl), Day1.AddDays(2), Guid.NewGuid());

        Assert.True(afterDisposal.IsSuccess);
        var result = afterDisposal.Value;
        Assert.Equal(90m, result.Quantity);
        Assert.Equal(288_000, result.CostBasis.AmountMinorUnits); // R$2.880,00
        Assert.Equal(3_200, result.AverageUnitCost.AmountMinorUnits); // still R$32,00
        Assert.Single(result.Disposals);
        Assert.Equal(2, result.Lots.Length); // both original lots remain, untouched
    }

    // INV-041: a disposal may not exceed the position's remaining quantity.
    [Fact]
    public void Disposing_more_than_the_remaining_quantity_fails()
    {
        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl)
            .Acquire(100m, Money.FromDecimal(30.00m, Commodity.Brl), Day1, Guid.NewGuid()).Value;

        var result = position.Dispose(101m, Money.FromDecimal(3_500.00m, Commodity.Brl), Day1.AddDays(1), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("POSITIONS.INSUFFICIENT_QUANTITY", result.Error.Code);
    }

    // Rounding safety: closing a position out entirely leaves exactly zero cost basis, whatever
    // half-even rounding did across the acquisitions that built up the average.
    [Fact]
    public void Disposing_the_entire_quantity_leaves_zero_cost_basis()
    {
        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl)
            .Acquire(100m, Money.FromDecimal(30.00m, Commodity.Brl), Day1, Guid.NewGuid()).Value
            .Acquire(50m, Money.FromDecimal(36.00m, Commodity.Brl), Day1.AddDays(1), Guid.NewGuid()).Value;

        var result = position.Dispose(150m, Money.FromDecimal(5_000.00m, Commodity.Brl), Day1.AddDays(2), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Quantity);
        Assert.Equal(0, result.Value.CostBasis.AmountMinorUnits);
    }

    [Fact]
    public void Acquiring_a_non_positive_quantity_fails()
    {
        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl);

        var result = position.Acquire(0m, Money.FromDecimal(30.00m, Commodity.Brl), Day1, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("POSITIONS.QUANTITY_MUST_BE_POSITIVE", result.Error.Code);
    }

    // INV-040: quantity always equals the net of every acquisition minus every disposal applied —
    // by construction, since Quantity only ever changes inside Acquire/Dispose.
    [Property]
    public bool Quantity_always_equals_acquired_minus_disposed(int acquired, int disposed)
    {
        var acquiredQty = Math.Abs(acquired) % 1000 + 1;
        var disposedQty = Math.Min(Math.Abs(disposed) % 1000 + 1, acquiredQty);

        var position = Position.Empty(TenantId.New(), Petr4, Commodity.Brl)
            .Acquire(acquiredQty, Money.FromDecimal(10.00m, Commodity.Brl), Day1, Guid.NewGuid()).Value;

        var afterDisposal = position.Dispose(disposedQty, Money.FromDecimal(10.00m * disposedQty, Commodity.Brl), Day1.AddDays(1), Guid.NewGuid());

        return afterDisposal.IsSuccess && afterDisposal.Value.Quantity == acquiredQty - disposedQty;
    }
}
