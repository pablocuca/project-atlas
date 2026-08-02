using Atlas.Modules.Ingestion.Application;
using Npgsql;

namespace Atlas.Modules.Ingestion.Infrastructure;

public sealed class ImportBatchRepository(NpgsqlDataSource dataSource) : IImportBatchRepository
{
    public async Task RecordAsync(ImportBatchRecord record, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO ingestion.import_batch
                (batch_id, tenant_id, source_id, blob_path, imported_at,
                 rows_parsed, entries_posted, duplicates_skipped, parse_failures, proposal_rejected)
            VALUES (@batchId, @tenantId, @sourceId, @blobPath, @importedAt,
                    @rowsParsed, @entriesPosted, @duplicatesSkipped, @parseFailures, @proposalRejected)
            """);

        command.Parameters.AddWithValue("batchId", record.BatchId);
        command.Parameters.AddWithValue("tenantId", record.TenantId);
        command.Parameters.AddWithValue("sourceId", record.SourceId);
        command.Parameters.AddWithValue("blobPath", record.BlobPath);
        command.Parameters.AddWithValue("importedAt", record.ImportedAt);
        command.Parameters.AddWithValue("rowsParsed", record.RowsParsed);
        command.Parameters.AddWithValue("entriesPosted", record.EntriesPosted);
        command.Parameters.AddWithValue("duplicatesSkipped", record.DuplicatesSkipped);
        command.Parameters.AddWithValue("parseFailures", record.ParseFailures);
        command.Parameters.AddWithValue("proposalRejected", record.ProposalRejected);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
