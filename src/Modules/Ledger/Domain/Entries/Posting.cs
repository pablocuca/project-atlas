using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Domain.Entries;

public enum PostingDirection
{
    Debit,
    Credit,
}

// Posting := (accountId, money, direction). Never exists alone — always a member of a JournalEntry.
// Decision 0001 §4: Money.AmountMinorUnits is always non-negative; Direction alone carries the sign.
public sealed record Posting
{
    public AccountId AccountId { get; }
    public Money Money { get; }
    public PostingDirection Direction { get; }

    private Posting(AccountId accountId, Money money, PostingDirection direction)
    {
        AccountId = accountId;
        Money = money;
        Direction = direction;
    }

    public static Result<Posting> Create(AccountId accountId, Money money, PostingDirection direction)
    {
        if (money.AmountMinorUnits < 0)
            return Result.Fail<Posting>(LedgerDomainErrors.PostingAmountMustBeNonNegative);

        return Result.Ok(new Posting(accountId, money, direction));
    }

    // Used to check BR-100 (per-commodity balance): Σ SignedMinorUnits() == 0 for each commodity.
    public long SignedMinorUnits() => Direction == PostingDirection.Debit ? Money.AmountMinorUnits : -Money.AmountMinorUnits;

    // Decision 0001 §1: a reversal posting is the same non-negative amount with direction flipped —
    // that flip is exactly the algebraic negation in signed terms.
    internal Posting Reversed() =>
        new(AccountId, Money, Direction == PostingDirection.Debit ? PostingDirection.Credit : PostingDirection.Debit);
}
