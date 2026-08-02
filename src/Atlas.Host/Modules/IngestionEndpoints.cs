using Atlas.Kernel;
using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Domain;

namespace Atlas.Host.Modules;

internal sealed record ColumnMappingRequest(
    Guid PrimaryAccountId, Guid UnclassifiedAccountId, string Commodity,
    int DateColumnIndex, int DescriptionColumnIndex, int AmountColumnIndex, bool HasHeaderRow);

internal sealed record ImportCsvRequest(
    Guid TenantId, string SourceId, string CsvContent, ColumnMappingRequest ColumnMapping,
    DateTimeOffset DecisionTime, DateTimeOffset CurrentTradingDayClose);

internal sealed record ImportOfxRequest(
    Guid TenantId, string SourceId, string OfxContent, Guid PrimaryAccountId, Guid UnclassifiedAccountId, string Commodity,
    DateTimeOffset DecisionTime, DateTimeOffset CurrentTradingDayClose);

internal sealed record ParseFailureResponse(int RowNumber, string RawLine, string Reason);

internal sealed record ImportResultResponse(
    Guid BatchId, string BlobPath, int RowsParsed, IReadOnlyList<ParseFailureResponse> ParseFailures,
    int EntriesPosted, int DuplicatesSkipped, int ProposalRejected, int DuplicateCandidatesFlagged);

internal sealed record ReconcileSourceRequest(
    Guid TenantId, string SourceId, Guid AccountId, string Commodity, long ReportedAmountMinorUnits,
    DateTimeOffset AsOfValidTime, DateTimeOffset AsOfDecisionTime);

internal sealed record ReconciliationResponse(
    long ReportedMinorUnits, long LedgerMinorUnits, long DiscrepancyMinorUnits, bool IsReconciled);

// docs/03-architecture/03-modular-monolith.md: minimal APIs, module-prefixed. No file upload
// (docs/decisions on this slice): the content comes as a plain JSON string field, matching every
// other endpoint in this codebase — there's no UI yet to drive a multipart upload.
internal static class IngestionEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ingestion");
        group.MapPost("/csv-imports", ImportCsv);
        group.MapPost("/ofx-imports", ImportOfx);
        group.MapPost("/reconciliations", ReconcileSource);
    }

    private static async Task<IResult> ImportCsv(
        ImportCsvRequest request, ImportCsvHandler handler, CancellationToken cancellationToken)
    {
        var mapping = new ColumnMapping(
            request.ColumnMapping.PrimaryAccountId, request.ColumnMapping.UnclassifiedAccountId, request.ColumnMapping.Commodity,
            request.ColumnMapping.DateColumnIndex, request.ColumnMapping.DescriptionColumnIndex,
            request.ColumnMapping.AmountColumnIndex, request.ColumnMapping.HasHeaderRow);

        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), request.SourceId, new RawPayload(request.CsvContent), mapping,
            new DecisionTime(request.DecisionTime), request.CurrentTradingDayClose, cancellationToken);

        return Results.Ok(new ImportResultResponse(
            result.BatchId, result.BlobPath, result.RowsParsed,
            [.. result.ParseFailures.Select(f => new ParseFailureResponse(f.RowNumber, f.RawLine, f.Reason))],
            result.EntriesPosted, result.DuplicatesSkipped, result.ProposalRejected, result.DuplicateCandidatesFlagged));
    }

    // FR-108. Same JSON-string-content decision as ImportCsv, and the same result shape — a client
    // doesn't need to know which statement format it imported to interpret the response.
    private static async Task<IResult> ImportOfx(
        ImportOfxRequest request, ImportOfxHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), request.SourceId, new RawPayload(request.OfxContent),
            request.PrimaryAccountId, request.UnclassifiedAccountId, request.Commodity,
            new DecisionTime(request.DecisionTime), request.CurrentTradingDayClose, cancellationToken);

        return Results.Ok(new ImportResultResponse(
            result.BatchId, result.BlobPath, result.RowsParsed,
            [.. result.ParseFailures.Select(f => new ParseFailureResponse(f.RowNumber, f.RawLine, f.Reason))],
            result.EntriesPosted, result.DuplicatesSkipped, result.ProposalRejected, result.DuplicateCandidatesFlagged));
    }

    // FR-111, BR-108: computes and records the discrepancy; never creates or adjusts a ledger
    // entry, whatever the outcome.
    private static async Task<IResult> ReconcileSource(
        ReconcileSourceRequest request, ReconcileSourceHandler handler, CancellationToken cancellationToken)
    {
        var reportedBalance = Money.FromMinorUnits(request.ReportedAmountMinorUnits, Commodity.BySymbol(request.Commodity));

        var outcome = await handler.HandleAsync(
            new TenantId(request.TenantId), request.SourceId, request.AccountId, reportedBalance,
            new ValidTime(request.AsOfValidTime), new DecisionTime(request.AsOfDecisionTime), cancellationToken);

        return Results.Ok(new ReconciliationResponse(
            outcome.ReportedBalance.AmountMinorUnits, outcome.LedgerBalance.AmountMinorUnits,
            outcome.DiscrepancyMinorUnits, outcome.IsReconciled));
    }
}
