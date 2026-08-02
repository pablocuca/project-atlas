namespace Atlas.Modules.Ingestion.Application;

public sealed record ImportBatchRecord(
    Guid BatchId,
    Guid TenantId,
    string SourceId,
    string BlobPath,
    DateTimeOffset ImportedAt,
    int RowsParsed,
    int EntriesPosted,
    int DuplicatesSkipped,
    int ParseFailures,
    int ProposalRejected);

// Auditability for C16 Ingestion's owned ImportBatch (docs/02-domain/02-bounded-contexts.md) — a
// summary record, not per-row storage; Ledger already keeps every posted entry, and the raw payload
// itself (not just its summary) lives in blob storage via IRawPayloadArchive.
public interface IImportBatchRepository
{
    Task RecordAsync(ImportBatchRecord record, CancellationToken cancellationToken);
}
