using Atlas.Kernel;
using Atlas.Modules.Cashflow.Domain;
using Atlas.Modules.Ledger.Contracts;

namespace Atlas.Modules.Cashflow.Application;

// FR-301, INV-060, US-013. A category is, this milestone, exactly a Ledger Expense-type account
// (Decision 0011) — there is no separate Category taxonomy yet, so classifying one means validating
// it through Ledger's own IFindAccount OHS port before recording the decision.
public sealed class ClassifyCategoryHandler(IFindAccount accounts, IClassificationRepository decisions)
{
    public async Task<Result<ClassificationDecision>> HandleAsync(
        TenantId tenantId, Guid categoryAccountId, Classification classification, string? rationale,
        DateTimeOffset decidedAt, CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(tenantId, categoryAccountId, cancellationToken);
        if (account is null)
            return Result.Fail<ClassificationDecision>(CashflowDomainErrors.CategoryAccountNotFound);
        if (account.Type != "Expense")
            return Result.Fail<ClassificationDecision>(CashflowDomainErrors.CategoryAccountNotAnExpense);

        var decision = ClassificationDecision.Create(tenantId, categoryAccountId, classification, rationale, decidedAt);
        await decisions.RecordAsync(decision, cancellationToken);

        return Result.Ok(decision);
    }
}
