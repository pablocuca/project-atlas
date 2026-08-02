namespace Atlas.Modules.Ingestion.Application;

public sealed record ReconciliationRecord(
    Guid ReconciliationId,
    Guid TenantId,
    string SourceId,
    Guid AccountId,
    string Commodity,
    DateTimeOffset AsOfValidTime,
    DateTimeOffset AsOfDecisionTime,
    long ReportedMinorUnits,
    long LedgerMinorUnits,
    long DiscrepancyMinorUnits,
    bool IsReconciled,
    DateTimeOffset ReconciledAt);

public interface IReconciliationRepository
{
    Task RecordAsync(ReconciliationRecord record, CancellationToken cancellationToken);
}
