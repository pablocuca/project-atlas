using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ingestion.Domain;
using Atlas.Modules.Ledger.Contracts;

namespace Atlas.Modules.Ingestion.Application;

// FR-108. Mirrors ImportCsvHandler's pipeline exactly (archive -> parse -> propose+post -> fuzzy
// dedup -> record batch) — see that class's comments for the stage-by-stage reasoning, which applies
// unchanged here. The only difference is the PARSE stage (OfxParser instead of CsvParser) and that
// OFX has no per-file column mapping to supply, just the three fields any statement import needs
// (Decision 0012): which account this statement is for, its unclassified counter-account, and its
// commodity.
public sealed class ImportOfxHandler(
    IRawPayloadArchive archive,
    IImportBatchRepository batches,
    IDuplicateCandidateRepository duplicateCandidates,
    IPostJournalEntry postJournalEntry,
    IFindEntriesInRange findEntriesInRange)
{
    private const int DuplicateWindowDays = 2;

    public async Task<ImportResult> HandleAsync(
        TenantId tenantId,
        string sourceId,
        RawPayload payload,
        Guid primaryAccountId,
        Guid unclassifiedAccountId,
        string commodity,
        DecisionTime decisionTime,
        DateTimeOffset currentTradingDayClose,
        CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid();
        var blobPath = await archive.ArchiveAsync(tenantId.Value, sourceId, "ofx", payload, cancellationToken);

        var (parsedRows, parseFailures) = OfxParser.Parse(payload);

        // EntryProposalBuilder.FromParsedRow reads only Commodity/PrimaryAccountId/
        // UnclassifiedAccountId off a ColumnMapping — never the four column-index/header fields,
        // which OFX has no equivalent of. Verified at that call site; not duplicating its ~10 lines
        // of proposal-building logic here for a second statement format is worth four inert values.
        var mapping = new ColumnMapping(
            primaryAccountId, unclassifiedAccountId, commodity, DateColumnIndex: 0, DescriptionColumnIndex: 0, AmountColumnIndex: 0, HasHeaderRow: false);

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
