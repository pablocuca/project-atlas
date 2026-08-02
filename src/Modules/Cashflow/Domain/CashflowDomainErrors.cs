using Atlas.Kernel;

namespace Atlas.Modules.Cashflow.Domain;

public static class CashflowDomainErrors
{
    public static readonly DomainError CategoryAccountNotFound = DomainError.Of(
        "CASHFLOW.CATEGORY_ACCOUNT_NOT_FOUND", "The account to classify does not exist.");

    // A category is, this milestone, exactly a Ledger Expense-type account (Decision 0011) — there
    // is no separate Category taxonomy yet.
    public static readonly DomainError CategoryAccountNotAnExpense = DomainError.Of(
        "CASHFLOW.CATEGORY_ACCOUNT_NOT_AN_EXPENSE", "Only Expense-type accounts can be classified.");
}
