using Atlas.Modules.Cashflow.Domain;

namespace Atlas.Modules.Cashflow.Application;

public static class ClassificationHistoryExtensions
{
    // A category's current classification is simply its most recent decision — there is no separate
    // "current state" column to keep in sync (see ClassificationDecision's own comment).
    public static ClassificationDecision? Current(this IReadOnlyList<ClassificationDecision> history) =>
        history.OrderByDescending(d => d.DecidedAt).FirstOrDefault();
}
