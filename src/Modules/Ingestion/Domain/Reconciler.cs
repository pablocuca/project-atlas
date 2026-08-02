using Atlas.Kernel;

namespace Atlas.Modules.Ingestion.Domain;

public sealed record ReconciliationOutcome(Money ReportedBalance, Money LedgerBalance, long DiscrepancyMinorUnits, bool IsReconciled);

// Stage 8, RECONCILE (docs/03-architecture/05-ingestion-and-integration.md §3): "discrepancy >
// tolerance => data-quality SLI breach." BR-108: never a silent adjustment — this type has no
// method that could create or change a ledger entry; it only classifies, and Reconciler.Reconcile
// is a pure comparison with nothing to auto-correct with even if it wanted to.
public static class Reconciler
{
    // No formal tolerance value exists anywhere in the business-rules or domain-model docs — only
    // in the M1 exit gate's prose ("reconciled to <= R$1,00"). Adopted as BR-108's actual tolerance:
    // one major unit of the commodity being reconciled (R$1,00 for BRL), not a fixed centavo count,
    // so it scales correctly for any commodity's minor-unit precision. Recorded in Decision 0008.
    public static ReconciliationOutcome Reconcile(Money reportedBalance, Money ledgerBalance)
    {
        if (reportedBalance.Commodity != ledgerBalance.Commodity)
            throw new InvalidOperationException(
                $"Cannot reconcile balances in different commodities: '{reportedBalance.Commodity.Symbol}' and '{ledgerBalance.Commodity.Symbol}'.");

        var tolerance = OneMajorUnit(reportedBalance.Commodity);
        var discrepancy = Math.Abs(reportedBalance.AmountMinorUnits - ledgerBalance.AmountMinorUnits);
        var isReconciled = discrepancy <= tolerance;

        return new ReconciliationOutcome(reportedBalance, ledgerBalance, discrepancy, isReconciled);
    }

    private static long OneMajorUnit(Commodity commodity)
    {
        long result = 1;
        for (var i = 0; i < commodity.MinorUnitScale; i++)
            result = checked(result * 10);
        return result;
    }
}
