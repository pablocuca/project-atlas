using Atlas.Kernel;
using Atlas.Modules.Cashflow.Domain;

namespace Atlas.Modules.Cashflow.Application;

public interface IClassificationRepository
{
    // Append-only (INV-060) — there is no Update or Delete on this interface, on purpose.
    Task RecordAsync(ClassificationDecision decision, CancellationToken cancellationToken);

    // Every decision ever made for this category, oldest first — the audit trail itself.
    Task<IReadOnlyList<ClassificationDecision>> FindHistoryAsync(
        TenantId tenantId, Guid categoryAccountId, CancellationToken cancellationToken);
}
