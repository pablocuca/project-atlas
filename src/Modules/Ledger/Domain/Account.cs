using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Domain;

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
}

// A named node in the chart of accounts.
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,
}

// Account := (id, tenantId, code, name, type, commodity, parentId?, openedAt, closedAt?).
// INV-020 / BR-105: type is immutable after first posting — enforced by never exposing a way to
// change it at all, for any Account, ever. There is no ChangeType method to call.
public sealed record Account
{
    public AccountId Id { get; }
    public TenantId TenantId { get; }
    public string Code { get; }
    public string Name { get; }
    public AccountType Type { get; }
    public Commodity Commodity { get; }
    public AccountId? ParentId { get; }
    public DateTimeOffset OpenedAt { get; }
    public DateTimeOffset? ClosedAt { get; }

    private Account(
        AccountId id,
        TenantId tenantId,
        string code,
        string name,
        AccountType type,
        Commodity commodity,
        AccountId? parentId,
        DateTimeOffset openedAt,
        DateTimeOffset? closedAt)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Type = type;
        Commodity = commodity;
        ParentId = parentId;
        OpenedAt = openedAt;
        ClosedAt = closedAt;
    }

    // Tree depth (≤ 6, INV-022) and per-tenant code uniqueness are set-level invariants that need
    // visibility across the whole chart of accounts — they belong to the Application layer /
    // repository, which has that visibility. This factory validates only what a single Account can
    // know about itself.
    public static Result<Account> Open(
        TenantId tenantId,
        string code,
        string name,
        AccountType type,
        Commodity commodity,
        AccountId? parentId,
        DateTimeOffset openedAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Fail<Account>(LedgerDomainErrors.AccountCodeRequired);
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail<Account>(LedgerDomainErrors.AccountNameRequired);

        return Result.Ok(new Account(AccountId.New(), tenantId, code, name, type, commodity, parentId, openedAt, null));
    }

    // BR-106 / INV-021: an account may not be closed while it holds a non-zero balance. The caller
    // supplies the current balance (computed via LedgerReplay) — Account itself has no visibility
    // into the ledger.
    public Result<Account> Close(DateTimeOffset closedAt, Money currentBalance)
    {
        if (ClosedAt is not null)
            return Result.Fail<Account>(LedgerDomainErrors.AccountAlreadyClosed);
        if (currentBalance.AmountMinorUnits != 0)
            return Result.Fail<Account>(LedgerDomainErrors.AccountHasNonZeroBalance);
        if (closedAt < OpenedAt)
            return Result.Fail<Account>(LedgerDomainErrors.AccountClosedBeforeOpened);

        return Result.Ok(new Account(Id, TenantId, Code, Name, Type, Commodity, ParentId, OpenedAt, closedAt));
    }

    // For Infrastructure to rehydrate a row it already knows is valid (it passed Open/Close on the
    // way in). Bypasses validation deliberately — re-running business rules against already-true
    // history would be wrong, not just redundant (e.g. a since-tightened rule shouldn't reject data
    // that was valid when it was written).
    public static Account Reconstitute(
        AccountId id,
        TenantId tenantId,
        string code,
        string name,
        AccountType type,
        Commodity commodity,
        AccountId? parentId,
        DateTimeOffset openedAt,
        DateTimeOffset? closedAt) =>
        new(id, tenantId, code, name, type, commodity, parentId, openedAt, closedAt);
}
