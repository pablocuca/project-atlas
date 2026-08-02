namespace Atlas.Seed;

public sealed record SyntheticAccount(string Code, string Name, string Type, DateTimeOffset OpenedAt);

// AccountCode, not an id — the server assigns real AccountIds on creation (AccountId.New()), which
// can't be predicted client-side. Postings reference the account by its (client-chosen) code and
// get resolved to the server's real id at post time.
public sealed record SyntheticPosting(string AccountCode, long AmountMinorUnits, string Direction);

public sealed record SyntheticCorrection(
    string Description, DateTimeOffset DecisionTime, IReadOnlyList<SyntheticPosting> Postings);

public sealed record SyntheticEntry(
    string IdempotencyKey,
    DateTimeOffset ValidTime,
    DateTimeOffset DecisionTime,
    string Description,
    IReadOnlyList<SyntheticPosting> Postings,
    SyntheticCorrection? Correction);

public sealed record SyntheticLife(Guid TenantId, IReadOnlyList<SyntheticAccount> Accounts, IReadOnlyList<SyntheticEntry> Entries);
