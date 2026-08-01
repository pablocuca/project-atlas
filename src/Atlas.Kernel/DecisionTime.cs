namespace Atlas.Kernel;

// When Atlas learned a fact (e.g. when the broker file was imported). Always passed in — never
// read from a clock (CS-2). INV-033: monotonically non-decreasing is enforced by the caller that
// assigns it (typically "now" at the moment of ingestion), not by this type.
public readonly record struct DecisionTime(DateTimeOffset Value) : IComparable<DecisionTime>
{
    public int CompareTo(DecisionTime other) => Value.CompareTo(other.Value);

    public static bool operator <(DecisionTime left, DecisionTime right) => left.CompareTo(right) < 0;
    public static bool operator >(DecisionTime left, DecisionTime right) => left.CompareTo(right) > 0;
    public static bool operator <=(DecisionTime left, DecisionTime right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DecisionTime left, DecisionTime right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString("O");
}
