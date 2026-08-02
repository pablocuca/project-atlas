using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ledger.Contracts;

namespace Atlas.Modules.Ingestion.Application;

public sealed record ImportResult(
    Guid BatchId,
    string BlobPath,
    int RowsParsed,
    ImmutableArray<ParseFailure> ParseFailures,
    int EntriesPosted,
    int DuplicatesSkipped,
    int ProposalRejected);

// Orchestrates the pipeline (docs/03-architecture/05-ingestion-and-integration.md §3) from CAPTURE
// through POST: archive the raw payload first (stage 1, "the most important stage" — it happens
// even if every row fails to parse), then PARSE, then PROPOSE + POST per row. Decision 0006: no
// CONFIRM-stage confidence gate this slice — every successfully proposed row posts directly.
public sealed class ImportCsvHandler(
    IRawPayloadArchive archive, IImportBatchRepository batches, IPostJournalEntry postJournalEntry)
{
    public async Task<ImportResult> HandleAsync(
        TenantId tenantId,
        string sourceId,
        RawPayload payload,
        ColumnMapping mapping,
        DecisionTime decisionTime,
        DateTimeOffset currentTradingDayClose,
        CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid();
        var blobPath = await archive.ArchiveAsync(tenantId.Value, sourceId, payload, cancellationToken);

        var (parsedRows, parseFailures) = CsvParser.Parse(payload, mapping);

        var posted = 0;
        var duplicates = 0;
        var proposalRejected = 0;

        foreach (var row in parsedRows)
        {
            var proposal = EntryProposalBuilder.FromParsedRow(row, mapping, sourceId);
            if (proposal.IsFailure)
            {
                proposalRejected++;
                continue;
            }

            var command = ToCommand(tenantId, decisionTime, currentTradingDayClose, sourceId, proposal.Value);
            var result = await postJournalEntry.PostAsync(command, cancellationToken);

            if (result.IsSuccess)
            {
                posted++;
            }
            else if (result.Error.Code == "LEDGER.DUPLICATE_IDEMPOTENCY_KEY")
            {
                // BR-103, exercised here exactly as atlas-seed already proved it for Ledger directly
                // — re-importing an overlapping window creates zero duplicates.
                duplicates++;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Row {row.RowNumber} failed to post for a reason other than a duplicate: {result.Error.Code} {result.Error.Message}");
            }
        }

        await batches.RecordAsync(
            new ImportBatchRecord(
                batchId, tenantId.Value, sourceId, blobPath, decisionTime.Value,
                parsedRows.Length, posted, duplicates, parseFailures.Length, proposalRejected),
            cancellationToken);

        return new ImportResult(batchId, blobPath, parsedRows.Length, parseFailures, posted, duplicates, proposalRejected);
    }

    private static PostJournalEntryCommand ToCommand(
        TenantId tenantId, DecisionTime decisionTime, DateTimeOffset currentTradingDayClose, string sourceId, EntryProposal proposal) =>
        new(
            tenantId, proposal.ValidTime, decisionTime, currentTradingDayClose, proposal.Description, sourceId, proposal.IdempotencyKey,
            [.. proposal.Postings.Select(p => new PostingCommand(p.AccountId, p.Money, p.Direction))]);
}
