using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Domain;

public static class LedgerDomainErrors
{
    public static readonly DomainError EntryTooFewPostings = DomainError.Of(
        "LEDGER.ENTRY_TOO_FEW_POSTINGS", "A journal entry must have at least two postings.");

    public static readonly DomainError EntryIdempotencyKeyRequired = DomainError.Of(
        "LEDGER.ENTRY_IDEMPOTENCY_KEY_REQUIRED", "A journal entry must carry a non-empty idempotency key.");

    public static readonly DomainError EntryValidTimeInFuture = DomainError.Of(
        "LEDGER.ENTRY_VALID_TIME_IN_FUTURE", "ValidTime may not exceed the current trading day's close."); // BR-104

    public static DomainError EntryUnbalanced(Commodity commodity) => DomainError.Of(
        "LEDGER.ENTRY_UNBALANCED", $"Postings in {commodity.Symbol} do not sum to zero."); // BR-100

    public static readonly DomainError PostingAmountMustBeNonNegative = DomainError.Of(
        "LEDGER.POSTING_AMOUNT_NEGATIVE", "A posting's Money amount must be non-negative; Direction carries the sign.");

    public static readonly DomainError AccountCodeRequired = DomainError.Of(
        "LEDGER.ACCOUNT_CODE_REQUIRED", "An account must have a non-empty code.");

    public static readonly DomainError AccountNameRequired = DomainError.Of(
        "LEDGER.ACCOUNT_NAME_REQUIRED", "An account must have a non-empty name.");

    public static readonly DomainError AccountAlreadyClosed = DomainError.Of(
        "LEDGER.ACCOUNT_ALREADY_CLOSED", "This account is already closed.");

    public static readonly DomainError AccountHasNonZeroBalance = DomainError.Of(
        "LEDGER.ACCOUNT_NON_ZERO_BALANCE",
        "An account may not be closed while it holds a non-zero balance."); // BR-106

    public static readonly DomainError AccountClosedBeforeOpened = DomainError.Of(
        "LEDGER.ACCOUNT_CLOSED_BEFORE_OPENED", "An account's closedAt cannot precede its openedAt.");
}
