using Atlas.Kernel;
using Atlas.Modules.Cashflow.Application;
using Atlas.Modules.Cashflow.Domain;

namespace Atlas.Host.Modules;

internal sealed record ClassifyCategoryRequest(Guid TenantId, Classification Classification, string? Rationale, DateTimeOffset DecidedAt);

internal sealed record ClassificationDecisionResponse(Guid Id, Classification Classification, string? Rationale, DateTimeOffset DecidedAt);

internal sealed record CategoryClassificationResponse(
    ClassificationDecisionResponse? Current, IReadOnlyList<ClassificationDecisionResponse> History);

// FR-301, INV-060, US-013. "Category" is, this milestone, exactly a Ledger Expense-type account
// (Decision 0011) — there is no separate proposal endpoint: no classification heuristic exists yet
// to propose from, so this covers only US-013's confirm half.
internal static class CashflowEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/cashflow/categories");
        group.MapPost("/{accountId:guid}/classify", Classify);
        group.MapGet("/{accountId:guid}/classification", GetClassification);
    }

    private static async Task<IResult> Classify(
        Guid accountId, ClassifyCategoryRequest request, ClassifyCategoryHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), accountId, request.Classification, request.Rationale, request.DecidedAt, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/cashflow/categories/{accountId}/classification", ToResponse(result.Value))
            : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    }

    private static async Task<IResult> GetClassification(
        Guid accountId, Guid tenantId, IClassificationRepository decisions, CancellationToken cancellationToken)
    {
        var history = await decisions.FindHistoryAsync(new TenantId(tenantId), accountId, cancellationToken);
        var current = history.Current();

        return Results.Ok(new CategoryClassificationResponse(
            current is null ? null : ToResponse(current), [.. history.Select(ToResponse)]));
    }

    private static ClassificationDecisionResponse ToResponse(ClassificationDecision decision) =>
        new(decision.Id.Value, decision.Classification, decision.Rationale, decision.DecidedAt);
}
