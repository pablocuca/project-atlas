using Atlas.Kernel;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ledger.Contracts;

namespace Atlas.Modules.Ingestion.Application;

// FR-111, BR-108. Never creates or adjusts a ledger entry — it only classifies. A breach is
// recorded, not fixed; docs/03-architecture/05-ingestion-and-integration.md §8's own failure-mode
// table is explicit: "Reconciliation gap > tolerance | Data-quality incident with a runbook."
// Raising an actual incident/SLI breach needs the Reliability module (M4) that doesn't exist yet —
// this handler's honest scope this slice is: compute the discrepancy, record the outcome, tell the
// caller whether it reconciled. See docs/decisions/0008.
public sealed class ReconcileSourceHandler(IQueryLedgerBalance queryBalance, IReconciliationRepository reconciliations)
{
    public async Task<ReconciliationOutcome> HandleAsync(
        TenantId tenantId,
        string sourceId,
        Guid accountId,
        Money reportedBalance,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime,
        CancellationToken cancellationToken)
    {
        var ledgerBalance = await queryBalance.QueryAsync(
            tenantId, accountId, reportedBalance.Commodity, asOfValidTime, asOfDecisionTime, cancellationToken);

        var outcome = Reconciler.Reconcile(reportedBalance, ledgerBalance);

        await reconciliations.RecordAsync(
            new ReconciliationRecord(
                Guid.NewGuid(), tenantId.Value, sourceId, accountId, reportedBalance.Commodity.Symbol,
                asOfValidTime.Value, asOfDecisionTime.Value, reportedBalance.AmountMinorUnits, ledgerBalance.AmountMinorUnits,
                outcome.DiscrepancyMinorUnits, outcome.IsReconciled, asOfDecisionTime.Value),
            cancellationToken);

        return outcome;
    }
}
