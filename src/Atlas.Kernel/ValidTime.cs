namespace Atlas.Kernel;

// When a fact was true in the real world (e.g. trade date). Always passed in — never read from a
// clock (CS-2): ambient clock access in domain code destroys reproducibility (AI-2).
public readonly record struct ValidTime(DateTimeOffset Value) : IComparable<ValidTime>
{
    public int CompareTo(ValidTime other) => Value.CompareTo(other.Value);

    public static bool operator <(ValidTime left, ValidTime right) => left.CompareTo(right) < 0;
    public static bool operator >(ValidTime left, ValidTime right) => left.CompareTo(right) > 0;
    public static bool operator <=(ValidTime left, ValidTime right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ValidTime left, ValidTime right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString("O");
}
