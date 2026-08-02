using Atlas.Kernel;
using Atlas.Modules.Positions.Application;
using Atlas.Modules.Positions.Domain;

namespace Atlas.Host.Modules;

internal sealed record RegisterInstrumentRequest(string Symbol, CommodityKind Kind, int MinorUnitScale, string? Jurisdiction);

internal sealed record SyncPositionRequest(
    Guid TenantId, Guid PositionAccountId, Guid CashAccountId, string Commodity, string CostCommodity,
    DateTimeOffset AsOfValidTime, DateTimeOffset AsOfDecisionTime);

internal sealed record LotResponse(Guid Id, decimal Quantity, long UnitCostMinorUnits, DateTimeOffset AcquiredAt, Guid SourceEntryId);

internal sealed record DisposalResponse(decimal Quantity, long ProceedsMinorUnits, DateTimeOffset DisposedAt, Guid SourceEntryId);

internal sealed record PositionResponse(
    Guid Id, string Commodity, decimal Quantity, long CostBasisMinorUnits, long AverageUnitCostMinorUnits,
    IReadOnlyList<LotResponse> Lots, IReadOnlyList<DisposalResponse> Disposals);

// FR-201/FR-202 (docs/01-product/02-functional-requirements.md). No file upload / bulk trade import
// this slice — a trade is posted through the existing POST /ledger/entries (it's a journal entry
// like any other; see Decision 0010), and Positions only ever reads that back.
internal static class PositionsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/positions");
        group.MapPost("/instruments", RegisterInstrument);
        group.MapPost("/sync", Sync);
        group.MapGet("/{commodity}", GetPosition);
    }

    // There is no commodity master-data table yet (FR-205+, M2) — this is the explicit, caller-driven
    // seam by which a new tradable instrument becomes postable at all (Decision 0009).
    private static IResult RegisterInstrument(RegisterInstrumentRequest request)
    {
        Commodity.Register(Commodity.Create(request.Symbol, request.Kind, request.MinorUnitScale, request.Jurisdiction));
        return Results.NoContent();
    }

    private static async Task<IResult> Sync(
        SyncPositionRequest request, SyncPositionHandler handler, CancellationToken cancellationToken)
    {
        var position = await handler.HandleAsync(
            new TenantId(request.TenantId), request.PositionAccountId, request.CashAccountId,
            Commodity.BySymbol(request.Commodity), Commodity.BySymbol(request.CostCommodity),
            new ValidTime(request.AsOfValidTime), new DecisionTime(request.AsOfDecisionTime), cancellationToken);

        return Results.Ok(ToResponse(position));
    }

    private static async Task<IResult> GetPosition(
        string commodity, Guid tenantId, IPositionRepository positions, CancellationToken cancellationToken)
    {
        var position = await positions.FindAsync(new TenantId(tenantId), Commodity.BySymbol(commodity), cancellationToken);
        return position is null ? Results.NotFound() : Results.Ok(ToResponse(position));
    }

    private static PositionResponse ToResponse(Position position) => new(
        position.Id.Value, position.Commodity.Symbol, position.Quantity,
        position.CostBasis.AmountMinorUnits, position.AverageUnitCost.AmountMinorUnits,
        [.. position.Lots.Select(l => new LotResponse(l.Id.Value, l.Quantity, l.UnitCost.AmountMinorUnits, l.AcquiredAt, l.SourceEntryId))],
        [.. position.Disposals.Select(d => new DisposalResponse(d.Quantity, d.Proceeds.AmountMinorUnits, d.DisposedAt, d.SourceEntryId))]);
}
