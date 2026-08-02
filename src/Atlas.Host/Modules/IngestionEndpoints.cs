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

internal sealed record ParseFailureResponse(int RowNumber, string RawLine, string Reason);

internal sealed record ImportResultResponse(
    Guid BatchId, string BlobPath, int RowsParsed, IReadOnlyList<ParseFailureResponse> ParseFailures,
    int EntriesPosted, int DuplicatesSkipped, int ProposalRejected, int DuplicateCandidatesFlagged);

// docs/03-architecture/03-modular-monolith.md: minimal APIs, module-prefixed. No file upload
// (docs/decisions on this slice): the content comes as a plain JSON string field, matching every
// other endpoint in this codebase — there's no UI yet to drive a multipart upload.
internal static class IngestionEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ingestion");
        group.MapPost("/csv-imports", ImportCsv);
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
}
