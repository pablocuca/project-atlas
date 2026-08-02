using Atlas.Modules.Ingestion.Application;
using Npgsql;

namespace Atlas.Modules.Ingestion.Infrastructure;

public sealed class ReconciliationRepository(NpgsqlDataSource dataSource) : IReconciliationRepository
{
    public async Task RecordAsync(ReconciliationRecord record, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO ingestion.reconciliation
                (reconciliation_id, tenant_id, source_id, account_id, commodity, as_of_valid_time, as_of_decision_time,
                 reported_minor_units, ledger_minor_units, discrepancy_minor_units, is_reconciled, reconciled_at)
            VALUES (@reconciliationId, @tenantId, @sourceId, @accountId, @commodity, @asOfValidTime, @asOfDecisionTime,
                    @reportedMinorUnits, @ledgerMinorUnits, @discrepancyMinorUnits, @isReconciled, @reconciledAt)
            """);

        command.Parameters.AddWithValue("reconciliationId", record.ReconciliationId);
        command.Parameters.AddWithValue("tenantId", record.TenantId);
        command.Parameters.AddWithValue("sourceId", record.SourceId);
        command.Parameters.AddWithValue("accountId", record.AccountId);
        command.Parameters.AddWithValue("commodity", record.Commodity);
        command.Parameters.AddWithValue("asOfValidTime", record.AsOfValidTime);
        command.Parameters.AddWithValue("asOfDecisionTime", record.AsOfDecisionTime);
        command.Parameters.AddWithValue("reportedMinorUnits", record.ReportedMinorUnits);
        command.Parameters.AddWithValue("ledgerMinorUnits", record.LedgerMinorUnits);
        command.Parameters.AddWithValue("discrepancyMinorUnits", record.DiscrepancyMinorUnits);
        command.Parameters.AddWithValue("isReconciled", record.IsReconciled);
        command.Parameters.AddWithValue("reconciledAt", record.ReconciledAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
