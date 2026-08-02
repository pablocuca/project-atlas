using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Positions.Domain;

namespace Atlas.Modules.Positions.Application;

// FR-201/FR-202, R03 (docs/02-domain/03-context-map.md: Ledger -> Position & Valuation, PL/OHS).
// No real event bus exists yet (IEventBusBuilder has no dispatcher) so Positions doesn't subscribe
// to JournalEntryPosted as a stream — it pulls through the same IFindEntriesInRange port Ingestion's
// fuzzy duplicate detection already proved (Decision 0010), and rebuilds a Position from scratch on
// every sync. That's not a shortcut: ADR-0018 already requires Positions to be "fully rebuildable"
// from the Ledger, so a pull-and-replace projection is the target design, not a stand-in for one.
public sealed class SyncPositionHandler(IFindEntriesInRange ledgerEntries, IPositionRepository positions)
{
    // positionAccountId/cashAccountId identify the two accounts whose postings are the real trade
    // legs — BR-100 requires every commodity's postings within an entry to net to zero, so a
    // buy/sell entry necessarily carries a second, clearing leg in each commodity too (INV-030).
    // Picking legs by account rather than "the posting in this commodity" is what lets Positions
    // find the right leg out of two same-commodity postings without Ledger needing a lotRef field
    // (Decision 0010).
    public async Task<Position> HandleAsync(
        TenantId tenantId, Guid positionAccountId, Guid cashAccountId, Commodity commodity, Commodity costCommodity,
        ValidTime asOfValidTime, DecisionTime asOfDecisionTime, CancellationToken cancellationToken)
    {
        var entries = await ledgerEntries.FindOriginalsInRangeAsync(
            tenantId, new ValidTime(DateTimeOffset.MinValue), asOfValidTime, cancellationToken);

        var trades = entries
            .Where(entry => entry.DecisionTime.Value <= asOfDecisionTime.Value)
            .Where(entry => entry.Postings.Any(p => p.AccountId == positionAccountId))
            .OrderBy(entry => entry.ValidTime.Value)
            .ThenBy(entry => entry.EntryId);

        var position = Position.Empty(tenantId, commodity, costCommodity);

        foreach (var entry in trades)
        {
            var instrumentLeg = entry.Postings.Single(p => p.AccountId == positionAccountId);
            var cashLeg = entry.Postings.Single(p => p.AccountId == cashAccountId);

            var quantity = ToDecimalAmount(instrumentLeg.Money);

            var applied = instrumentLeg.Direction switch
            {
                "Debit" => position.Acquire(
                    quantity, UnitCost(cashLeg.Money, quantity), entry.ValidTime.Value, entry.EntryId),
                "Credit" => position.Dispose(quantity, cashLeg.Money, entry.ValidTime.Value, entry.EntryId),
                var other => throw new InvalidOperationException($"Unrecognised posting direction '{other}'."),
            };

            if (applied.IsFailure)
                throw new InvalidOperationException(
                    $"Position sync failed replaying entry {entry.EntryId}: {applied.Error.Code} — {applied.Error.Message}");

            position = applied.Value;
        }

        await positions.ReplaceAsync(position, cancellationToken);
        return position;
    }

    private static decimal ToDecimalAmount(Money money) => money.AmountMinorUnits / Pow10(money.Commodity.MinorUnitScale);

    private static Money UnitCost(Money cash, decimal quantity) =>
        Money.FromDecimal(ToDecimalAmount(cash) / quantity, cash.Commodity);

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
            result *= 10m;

        return result;
    }
}
