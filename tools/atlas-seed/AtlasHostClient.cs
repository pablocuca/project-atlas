using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Atlas.Seed;

// A thin HTTP client for Atlas.Host's /ledger/* routes — the DTOs here match, but deliberately
// don't share, Atlas.Host's internal request/response shapes (docs/decisions/0004): this tool has
// no ProjectReference into Atlas.Host at all.
public sealed class AtlasHostClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Falls back to a by-code lookup if the account already exists (INV-022, ACCOUNT_CODE_ALREADY_IN_USE)
    // — expected and safe on a rerun against an already-seeded database, same treatment as the
    // duplicate-entry case below.
    public async Task<Guid> OpenAccountAsync(Guid tenantId, SyntheticAccount account, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/ledger/accounts", new
        {
            tenantId,
            code = account.Code,
            name = account.Name,
            type = account.Type,
            commodity = SyntheticLifeGenerator.Commodity,
            parentId = (Guid?)null,
            openedAt = account.OpenedAt,
        }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            if (error.Code == "LEDGER.ACCOUNT_CODE_ALREADY_IN_USE")
                return await FindAccountByCodeAsync(tenantId, account.Code, cancellationToken);

            throw new InvalidOperationException($"Failed to open account '{account.Code}': {error.Code} {error.Message}");
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccountResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Empty response opening account '{account.Code}'.");
        return body.Id;
    }

    // Returns null if the server rejected it as a duplicate (BR-103) — expected and safe on a
    // rerun against an already-seeded database (docs/decisions/0004). Throws for anything else.
    public async Task<Guid?> PostEntryAsync(
        Guid tenantId,
        IReadOnlyDictionary<string, Guid> accountIdsByCode,
        SyntheticEntry entry,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            tenantId,
            validTime = entry.ValidTime,
            decisionTime = entry.DecisionTime,
            currentTradingDayClose = entry.ValidTime.AddDays(1),
            description = entry.Description,
            sourceId = "atlas-seed",
            idempotencyKey = entry.IdempotencyKey,
            postings = ToPostingPayload(entry.Postings, accountIdsByCode),
        };

        var response = await httpClient.PostAsJsonAsync("/ledger/entries", payload, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            if (error.Code == "LEDGER.DUPLICATE_IDEMPOTENCY_KEY")
                return null;

            throw new InvalidOperationException($"Failed to post entry '{entry.Description}': {error.Code} {error.Message}");
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EntryResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Empty response posting entry '{entry.Description}'.");
        return body.Id;
    }

    // Returns false if already applied (the correction's derived idempotency keys are deterministic
    // too, so a rerun hits BR-103 here as well) — same "expected on rerun" treatment as PostEntryAsync.
    public async Task<bool> CorrectEntryAsync(
        Guid tenantId,
        Guid entryId,
        IReadOnlyDictionary<string, Guid> accountIdsByCode,
        SyntheticCorrection correction,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            tenantId,
            decisionTime = correction.DecisionTime,
            description = correction.Description,
            postings = ToPostingPayload(correction.Postings, accountIdsByCode),
        };

        var response = await httpClient.PostAsJsonAsync($"/ledger/entries/{entryId}/correct", payload, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            if (error.Code == "LEDGER.DUPLICATE_IDEMPOTENCY_KEY")
                return false;

            throw new InvalidOperationException($"Failed to correct entry {entryId}: {error.Code} {error.Message}");
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<long> GetBalanceAsync(
        Guid tenantId, Guid accountId, DateTimeOffset asOfValidTime, DateTimeOffset asOfDecisionTime, CancellationToken cancellationToken)
    {
        var uri = $"/ledger/accounts/{accountId}/balance" +
                  $"?tenantId={tenantId}&commodity={SyntheticLifeGenerator.Commodity}" +
                  $"&asOfValidTime={Uri.EscapeDataString(asOfValidTime.ToString("O"))}" +
                  $"&asOfDecisionTime={Uri.EscapeDataString(asOfDecisionTime.ToString("O"))}";

        var body = await httpClient.GetFromJsonAsync<BalanceResponseDto>(uri, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Empty balance response for account {accountId}.");
        return body.AmountMinorUnits;
    }

    private async Task<Guid> FindAccountByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var uri = $"/ledger/accounts/by-code/{Uri.EscapeDataString(code)}?tenantId={tenantId}";
        var body = await httpClient.GetFromJsonAsync<AccountResponseDto>(uri, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Account '{code}' reported as a duplicate but could not be found by code.");
        return body.Id;
    }

    private static IEnumerable<object> ToPostingPayload(
        IReadOnlyList<SyntheticPosting> postings, IReadOnlyDictionary<string, Guid> accountIdsByCode) =>
        postings.Select(p => new
        {
            accountId = accountIdsByCode[p.AccountCode],
            amountMinorUnits = p.AmountMinorUnits,
            commodity = SyntheticLifeGenerator.Commodity,
            direction = p.Direction,
        });

    private static async Task<ErrorResponseDto> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ErrorResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server returned 400 with no error body.");

    private sealed record AccountResponseDto(Guid Id);
    private sealed record EntryResponseDto(Guid Id);
    private sealed record ErrorResponseDto(string Code, string Message);
    private sealed record BalanceResponseDto(long AmountMinorUnits, string Commodity);
}
