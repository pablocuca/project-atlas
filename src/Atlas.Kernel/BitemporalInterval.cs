namespace Atlas.Kernel;

// BitemporalInterval := (from, to). Half-open [from, to) — to = null means "currently true" /
// "currently believed" (INV-014). Non-overlap for a given fact key at a single decision-time
// (INV-015) is a property of the sequence a caller builds, not of this type in isolation.
public readonly record struct BitemporalInterval
{
    public DateTimeOffset From { get; }
    public DateTimeOffset? To { get; }

    public BitemporalInterval(DateTimeOffset from, DateTimeOffset? to = null)
    {
        if (to is { } end && end <= from)
            throw new ArgumentException("A bitemporal interval's exclusive end must be after its start.", nameof(to));

        From = from;
        To = to;
    }

    public bool IsOpen => To is null;

    public bool Contains(DateTimeOffset instant) => instant >= From && (To is null || instant < To.Value);
}
