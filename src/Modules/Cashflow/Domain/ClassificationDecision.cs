using Atlas.Kernel;

namespace Atlas.Modules.Cashflow.Domain;

public readonly record struct ClassificationDecisionId(Guid Value)
{
    public static ClassificationDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

// classification ∈ {Essential, CommittedDiscretionary, Discretionary}
// (docs/02-domain/04-domain-model.md §5, CategoryModel).
public enum Classification
{
    Essential,
    CommittedDiscretionary,
    Discretionary,
}

// INV-060: classification is a user decision, versioned with an audit trail — the system may
// propose but never silently assign. There is no way to edit or delete a ClassificationDecision;
// reclassifying a category creates a new one (Decision 0011). A category's current classification is
// simply its most recent decision by DecidedAt — there is no separate "current state" to keep in
// sync, the same way Ledger has no mutable balance column.
public sealed record ClassificationDecision(
    ClassificationDecisionId Id, TenantId TenantId, Guid CategoryAccountId,
    Classification Classification, string? Rationale, DateTimeOffset DecidedAt)
{
    public static ClassificationDecision Create(
        TenantId tenantId, Guid categoryAccountId, Classification classification, string? rationale, DateTimeOffset decidedAt) =>
        new(ClassificationDecisionId.New(), tenantId, categoryAccountId, classification, rationale, decidedAt);
}
