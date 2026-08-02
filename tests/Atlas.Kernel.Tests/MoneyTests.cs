using Atlas.Kernel;
using FsCheck;
using FsCheck.Xunit;

namespace Atlas.Kernel.Tests;

public class MoneyTests
{
    // Testing Strategy §2: "Money split conservation — for any amount and divisor, the parts
    // re-sum to the original exactly" (INV-011 / BR-004).
    [Property]
    [BusinessRule("BR-004")]
    public bool Split_parts_always_resum_to_the_original(int amount, PositiveInt parts)
    {
        var money = Money.FromMinorUnits(amount, Commodity.Brl);
        var shares = money.Split(parts.Get);

        var resummed = shares.Aggregate(0L, (sum, share) => sum + share.AmountMinorUnits);
        return resummed == amount && shares.Length == parts.Get;
    }

    [Property]
    public bool Split_shares_never_differ_by_more_than_one_minor_unit(int amount, PositiveInt parts)
    {
        var money = Money.FromMinorUnits(amount, Commodity.Brl);
        var shares = money.Split(parts.Get).Select(s => s.AmountMinorUnits).ToArray();

        return shares.Max() - shares.Min() <= 1;
    }

    // Testing Strategy §2: "Money commodity safety — arithmetic on differing commodities always
    // throws" (INV-010 / BR-002).
    [Property]
    [BusinessRule("BR-002")]
    public bool Arithmetic_across_commodities_always_throws(int a, int b)
    {
        var brl = Money.FromMinorUnits(a, Commodity.Brl);
        var usd = Money.FromMinorUnits(b, Commodity.Usd);

        try
        {
            _ = brl.Add(usd);
            return false;
        }
        catch (CommodityMismatchException)
        {
            return true;
        }
    }

    // BR-003 / INV-005: rounding is half-even, applied exactly once, at the declared boundary.
    [Theory]
    [BusinessRuleTheoryData]
    [BusinessRule("BR-003")]
    public void FromDecimal_rounds_half_to_even_at_the_declared_boundary(decimal amount, long expectedMinorUnits)
    {
        var money = Money.FromDecimal(amount, Commodity.Brl);
        Assert.Equal(expectedMinorUnits, money.AmountMinorUnits);
    }

    [Fact]
    public void Add_throws_OverflowException_instead_of_wrapping()
    {
        var max = Money.FromMinorUnits(long.MaxValue, Commodity.Brl);
        var one = Money.FromMinorUnits(1, Commodity.Brl);

        Assert.Throws<OverflowException>(() => max.Add(one));
    }

    [Fact]
    public void Sum_of_an_empty_sequence_throws_because_a_commodity_is_required()
    {
        Assert.Throws<ArgumentException>(() => Money.Sum(Array.Empty<Money>()));
    }
}

public sealed class BusinessRuleTheoryDataAttribute : ClassDataAttribute
{
    public BusinessRuleTheoryDataAttribute() : base(typeof(RoundingCases))
    {
    }
}

public sealed class RoundingCases : TheoryData<decimal, long>
{
    public RoundingCases()
    {
        // BRL, scale 2. Half-even: ties round to the nearest even minor unit.
        Add(0.005m, 0);   // 0 is even
        Add(0.015m, 2);   // 2 is even
        Add(0.025m, 2);   // 2 is even
        Add(0.035m, 4);   // 4 is even
        Add(1.004m, 100);
        Add(1.006m, 101);
    }
}
