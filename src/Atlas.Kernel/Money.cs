using System.Collections.Immutable;

namespace Atlas.Kernel;

public enum Rounding
{
    HalfEven,
}

// INV-010 / BR-002: arithmetic between different commodities throws — never an implicit conversion.
public sealed class CommodityMismatchException : InvalidOperationException
{
    public CommodityMismatchException(Commodity left, Commodity right)
        : base($"Cannot operate on Money in different commodities: '{left.Symbol}' and '{right.Symbol}'.")
    {
        Left = left;
        Right = right;
    }

    public Commodity Left { get; }
    public Commodity Right { get; }
}

// Money := (amount: int64 minor units, commodity: Commodity). Never a floating-point number
// (INV-001, ADR-0003). Overflow throws OverflowException — a domain error, not a silent wrap (INV-012).
public readonly record struct Money
{
    public long AmountMinorUnits { get; }
    public Commodity Commodity { get; }

    private Money(long amountMinorUnits, Commodity commodity)
    {
        AmountMinorUnits = amountMinorUnits;
        Commodity = commodity;
    }

    public static Money Zero(Commodity commodity) => new(0, commodity);

    public static Money FromMinorUnits(long amountMinorUnits, Commodity commodity) => new(amountMinorUnits, commodity);

    // BR-003 / INV-005: the one place a fractional value becomes a Money — the declared rounding
    // boundary. Everything upstream of this call keeps full decimal precision.
    public static Money FromDecimal(decimal amount, Commodity commodity, Rounding rounding = Rounding.HalfEven)
    {
        var scale = Pow10(commodity.MinorUnitScale);
        var minorUnits = Math.Round(amount * scale, MidpointRounding.ToEven);
        return new Money(checked((long)minorUnits), commodity);
    }

    public static Money Sum(IEnumerable<Money> amounts)
    {
        Money? total = null;
        foreach (var amount in amounts)
            total = total is null ? amount : total.Value.Add(amount);

        return total ?? throw new ArgumentException(
            "Cannot sum an empty sequence of Money — a commodity is required.", nameof(amounts));
    }

    public Money Add(Money other)
    {
        EnsureSameCommodity(other);
        return new Money(checked(AmountMinorUnits + other.AmountMinorUnits), Commodity);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCommodity(other);
        return new Money(checked(AmountMinorUnits - other.AmountMinorUnits), Commodity);
    }

    public Money Negate() => new(checked(-AmountMinorUnits), Commodity);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    // INV-011 / BR-004: largest-remainder apportionment. Sum(Split(n)) always equals the original,
    // for any amount and any n > 0 — the remainder is distributed, never discarded.
    public ImmutableArray<Money> Split(int parts)
    {
        if (parts <= 0)
            throw new ArgumentOutOfRangeException(nameof(parts), parts, "Must split into at least one part.");

        var baseShare = AmountMinorUnits / parts;
        var remainder = (int)(AmountMinorUnits % parts);

        var builder = ImmutableArray.CreateBuilder<Money>(parts);
        for (var i = 0; i < parts; i++)
        {
            var share = baseShare;
            if (remainder > 0 && i < remainder)
                share += 1;
            else if (remainder < 0 && i < -remainder)
                share -= 1;

            builder.Add(new Money(share, Commodity));
        }

        return builder.MoveToImmutable();
    }

    private void EnsureSameCommodity(Money other)
    {
        if (Commodity != other.Commodity)
            throw new CommodityMismatchException(Commodity, other.Commodity);
    }

    private static long Pow10(int exponent)
    {
        long result = 1;
        for (var i = 0; i < exponent; i++)
            result = checked(result * 10);

        return result;
    }
}
