using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;

namespace Atlas.Modules.Ledger.Application;

// A port (docs/03-architecture/03-modular-monolith.md §1) — defined here, implemented by
// Ledger.Infrastructure. Insert/UpdateClosedAt return Result<Unit> rather than throwing, because a
// duplicate code or a concurrent close is an expected outcome the caller must branch on, not a bug
// (docs/05-engineering/02-coding-standards.md §2).
public interface IAccountRepository
{
    Task<Account?> FindByIdAsync(TenantId tenantId, AccountId accountId, CancellationToken cancellationToken);

    Task<Account?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);

    Task<Result<Unit>> InsertAsync(Account account, CancellationToken cancellationToken);

    // Only ClosedAt ever changes after insert — BR-105 (type immutable) is enforced by the database
    // role only being granted UPDATE on that one column (migration 001).
    Task<Result<Unit>> UpdateClosedAtAsync(Account account, CancellationToken cancellationToken);
}
