using Atlas.Modules.Ingestion.Application;
using Npgsql;

namespace Atlas.Modules.Ingestion.Infrastructure;

public sealed class DuplicateCandidateRepository(NpgsqlDataSource dataSource) : IDuplicateCandidateRepository
{
    public async Task RecordAsync(DuplicateCandidateRecord record, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO ingestion.duplicate_candidate
                (candidate_id, tenant_id, new_entry_id, existing_entry_id, similarity, detected_at)
            VALUES (@candidateId, @tenantId, @newEntryId, @existingEntryId, @similarity, @detectedAt)
            """);

        command.Parameters.AddWithValue("candidateId", record.CandidateId);
        command.Parameters.AddWithValue("tenantId", record.TenantId);
        command.Parameters.AddWithValue("newEntryId", record.NewEntryId);
        command.Parameters.AddWithValue("existingEntryId", record.ExistingEntryId);
        command.Parameters.AddWithValue("similarity", record.Similarity);
        command.Parameters.AddWithValue("detectedAt", record.DetectedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
