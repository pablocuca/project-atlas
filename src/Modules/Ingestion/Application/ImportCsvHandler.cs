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
    int ProposalRejected,
    int DuplicateCandidatesFlagged);

// Orchestrates the pipeline (docs/03-architecture/05-ingestion-and-integration.md §3) from CAPTURE
// through POST and the fuzzy-DEDUPLICATE check that runs alongside it: archive the raw payload
// first (stage 1, "the most important stage" — it happens even if every row fails to parse), then
// PARSE, then PROPOSE + POST per row, then check each posted entry against recent Ledger entries
// for a probable cross-source duplicate (FR-110). Decision 0006: no CONFIRM-stage confidence gate
// this slice — every successfully proposed row posts directly.
public sealed class ImportCsvHandler(
    IRawPayloadArchive archive,
    IImportBatchRepository batches,
    IDuplicateCandidateRepository duplicateCandidates,
    IPostJournalEntry postJournalEntry,
    IFindEntriesInRange findEntriesInRange)
{
    // docs/03-architecture/05-ingestion-and-integration.md §4: "date +/- 2 days."
    private const int DuplicateWindowDays = 2;

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
        var duplicateCandidatesFlagged = 0;

        var recentEntries = await LoadRecentEntriesAsync(tenantId, parsedRows, cancellationToken);

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

            if (result.IsFailure)
            {
                if (result.Error.Code == "LEDGER.DUPLICATE_IDEMPOTENCY_KEY")
                {
                    // BR-103, exercised here exactly as atlas-seed already proved it for Ledger
                    // directly — re-importing an overlapping window creates zero duplicates.
                    duplicates++;
                    continue;
                }

                throw new InvalidOperationException(
                    $"Row {row.RowNumber} failed to post for a reason other than a duplicate: {result.Error.Code} {result.Error.Message}");
            }

            posted++;
            var postedEntry = result.Value;
            var amount = AbsoluteAmountOf(postedEntry.Postings);

            var candidates = DuplicateDetector.FindCandidates(
                postedEntry.ValidTime.Value, postedEntry.Description, amount, postedEntry.EntryId, recentEntries);

            foreach (var candidate in candidates)
            {
                await duplicateCandidates.RecordAsync(
                    new DuplicateCandidateRecord(
                        Guid.NewGuid(), tenantId.Value, postedEntry.EntryId, candidate.ExistingEntryId, candidate.Similarity, decisionTime.Value),
                    cancellationToken);
                duplicateCandidatesFlagged++;
            }

            // Later rows in the same batch can also be a fuzzy duplicate of an earlier one — not
            // just of what was already in Ledger before this import started.
            recentEntries.Add(new ExistingEntrySummary(postedEntry.EntryId, postedEntry.ValidTime.Value, postedEntry.Description, amount));
        }

        await batches.RecordAsync(
            new ImportBatchRecord(
                batchId, tenantId.Value, sourceId, blobPath, decisionTime.Value,
                parsedRows.Length, posted, duplicates, parseFailures.Length, proposalRejected),
            cancellationToken);

        return new ImportResult(
            batchId, blobPath, parsedRows.Length, parseFailures, posted, duplicates, proposalRejected, duplicateCandidatesFlagged);
    }

    // One query covering the whole file's date range (+/- the fuzzy window), not one per row —
    // every row's fuzzy check runs against the same in-memory list.
    private async Task<List<ExistingEntrySummary>> LoadRecentEntriesAsync(
        TenantId tenantId, ImmutableArray<ParsedRow> parsedRows, CancellationToken cancellationToken)
    {
        if (parsedRows.IsEmpty)
            return [];

        var minDate = parsedRows.Min(r => r.Date);
        var maxDate = parsedRows.Max(r => r.Date);

        var existing = await findEntriesInRange.FindOriginalsInRangeAsync(
            tenantId,
            new ValidTime(minDate.AddDays(-DuplicateWindowDays)),
            new ValidTime(maxDate.AddDays(DuplicateWindowDays)),
            cancellationToken);

        return [.. existing.Select(e => new ExistingEntrySummary(e.EntryId, e.ValidTime.Value, e.Description, AbsoluteAmountOf(e.Postings)))];
    }

    private static long AbsoluteAmountOf(ImmutableArray<PostedPosting> postings) =>
        postings.Where(p => p.Direction == "Debit").Sum(p => p.Money.AmountMinorUnits);

    private static PostJournalEntryCommand ToCommand(
        TenantId tenantId, DecisionTime decisionTime, DateTimeOffset currentTradingDayClose, string sourceId, EntryProposal proposal) =>
        new(
            tenantId, proposal.ValidTime, decisionTime, currentTradingDayClose, proposal.Description, sourceId, proposal.IdempotencyKey,
            [.. proposal.Postings.Select(p => new PostingCommand(p.AccountId, p.Money, p.Direction))]);
}
