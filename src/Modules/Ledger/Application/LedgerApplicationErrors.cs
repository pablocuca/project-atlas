using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Application;

public static class LedgerApplicationErrors
{
    public static readonly DomainError AccountCodeAlreadyInUse = DomainError.Of(
        "LEDGER.ACCOUNT_CODE_ALREADY_IN_USE", "An account with this code already exists for this tenant."); // INV-022

    public static readonly DomainError AccountNotFound = DomainError.Of(
        "LEDGER.ACCOUNT_NOT_FOUND", "No account exists with this id for this tenant.");

    public static readonly DomainError EntryNotFound = DomainError.Of(
        "LEDGER.ENTRY_NOT_FOUND", "No journal entry exists with this id for this tenant.");

    public static readonly DomainError DuplicateIdempotencyKey = DomainError.Of(
        "LEDGER.DUPLICATE_IDEMPOTENCY_KEY", "An entry with this source and idempotency key already exists."); // BR-103
}
