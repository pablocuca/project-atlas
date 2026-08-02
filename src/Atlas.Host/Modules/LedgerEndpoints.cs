using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Application;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Host.Modules;

internal sealed record OpenAccountRequest(
    Guid TenantId, string Code, string Name, AccountType Type, string Commodity, Guid? ParentId, DateTimeOffset OpenedAt);

internal sealed record CloseAccountRequest(Guid TenantId, DateTimeOffset ClosedAt);

internal sealed record PostingRequest(Guid AccountId, long AmountMinorUnits, string Commodity, string Direction);

internal sealed record PostJournalEntryRequest(
    Guid TenantId, DateTimeOffset ValidTime, DateTimeOffset DecisionTime, DateTimeOffset CurrentTradingDayClose,
    string Description, string SourceId, string IdempotencyKey, IReadOnlyList<PostingRequest> Postings);

internal sealed record CorrectJournalEntryRequest(
    Guid TenantId, DateTimeOffset DecisionTime, string Description, IReadOnlyList<PostingRequest> Postings);

internal sealed record AccountResponse(
    Guid Id, string Code, string Name, string Type, string Commodity, Guid? ParentId,
    DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt);

internal sealed record JournalEntryResponse(
    Guid Id, DateTimeOffset ValidTime, DateTimeOffset DecisionTime, string Kind, Guid? CorrectsEntryId, string Description);

internal sealed record ErrorResponse(string Code, string Message);

// docs/03-architecture/03-modular-monolith.md: minimal APIs, module-prefixed. Each route is a thin
// translation of a Slice-2 Application handler's Result<T> to an HTTP response — no logic lives
// here beyond that translation.
internal static class LedgerEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ledger");

        group.MapPost("/accounts", OpenAccount);
        group.MapPost("/accounts/{id:guid}/close", CloseAccount);
        group.MapPost("/entries", PostEntry);
        group.MapPost("/entries/{id:guid}/correct", CorrectEntry);
        group.MapGet("/accounts/{id:guid}/balance", GetBalance);
    }

    private static async Task<IResult> OpenAccount(
        OpenAccountRequest request, OpenAccountHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), request.Code, request.Name, request.Type,
            Commodity.BySymbol(request.Commodity), request.ParentId is { } parentId ? new AccountId(parentId) : null,
            request.OpenedAt, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/ledger/accounts/{result.Value.Id.Value}", ToResponse(result.Value))
            : ToErrorResult(result.Error);
    }

    private static async Task<IResult> CloseAccount(
        Guid id, CloseAccountRequest request, CloseAccountHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), new AccountId(id), request.ClosedAt, cancellationToken);

        return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : ToErrorResult(result.Error);
    }

    private static async Task<IResult> PostEntry(
        PostJournalEntryRequest request, PostJournalEntryHandler handler, CancellationToken cancellationToken)
    {
        var postings = ToPostings(request.Postings);
        if (postings.IsFailure)
            return ToErrorResult(postings.Error);

        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), new ValidTime(request.ValidTime), new DecisionTime(request.DecisionTime),
            request.CurrentTradingDayClose, request.Description, request.SourceId, request.IdempotencyKey,
            postings.Value, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/ledger/entries/{result.Value.Entry.Id.Value}", ToResponse(result.Value.Entry))
            : ToErrorResult(result.Error);
    }

    private static async Task<IResult> CorrectEntry(
        Guid id, CorrectJournalEntryRequest request, CorrectJournalEntryHandler handler, CancellationToken cancellationToken)
    {
        var postings = ToPostings(request.Postings);
        if (postings.IsFailure)
            return ToErrorResult(postings.Error);

        var result = await handler.HandleAsync(
            new TenantId(request.TenantId), new EntryId(id), new DecisionTime(request.DecisionTime),
            request.Description, postings.Value, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new { reversal = ToResponse(result.Value.Reversal), replacement = ToResponse(result.Value.Replacement) })
            : ToErrorResult(result.Error);
    }

    private static async Task<IResult> GetBalance(
        Guid id, Guid tenantId, string commodity, DateTimeOffset asOfValidTime, DateTimeOffset asOfDecisionTime,
        BalanceAtHandler handler, CancellationToken cancellationToken)
    {
        var balance = await handler.HandleAsync(
            new TenantId(tenantId), new AccountId(id), Commodity.BySymbol(commodity),
            new ValidTime(asOfValidTime), new DecisionTime(asOfDecisionTime), cancellationToken);

        return Results.Ok(new { amountMinorUnits = balance.AmountMinorUnits, commodity = balance.Commodity.Symbol });
    }

    private static Result<ImmutableArray<Posting>> ToPostings(IReadOnlyList<PostingRequest> postings)
    {
        var builder = ImmutableArray.CreateBuilder<Posting>(postings.Count);
        foreach (var posting in postings)
        {
            var created = Posting.Create(
                new AccountId(posting.AccountId), Money.FromMinorUnits(posting.AmountMinorUnits, Commodity.BySymbol(posting.Commodity)),
                Enum.Parse<PostingDirection>(posting.Direction));

            if (created.IsFailure)
                return Result.Fail<ImmutableArray<Posting>>(created.Error);

            builder.Add(created.Value);
        }

        return Result.Ok(builder.ToImmutable());
    }

    private static AccountResponse ToResponse(Account account) => new(
        account.Id.Value, account.Code, account.Name, account.Type.ToString(), account.Commodity.Symbol,
        account.ParentId?.Value, account.OpenedAt, account.ClosedAt);

    private static JournalEntryResponse ToResponse(JournalEntry entry) => new(
        entry.Id.Value, entry.ValidTime.Value, entry.DecisionTime.Value, entry.Kind.ToString(),
        entry.CorrectsEntryId?.Value, entry.Description);

    private static IResult ToErrorResult(DomainError error) => Results.BadRequest(new ErrorResponse(error.Code, error.Message));
}
