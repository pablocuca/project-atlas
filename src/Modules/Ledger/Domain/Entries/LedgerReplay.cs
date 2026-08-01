using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Domain.Entries;

// BR-107: any ledger state must be reconstructible by replaying entries in DecisionTime order.
// INV-035: every balance query takes both time coordinates — there is deliberately no single-time
// overload, which is how "what changed since yesterday" stays answerable when data arrives late.
//
// This is the in-memory, pure-domain heart of the rule. It operates over whatever sequence of
// JournalEntry the caller supplies; a Postgres-backed store (arriving in a later slice) is
// responsible for producing that sequence efficiently, not for the replay logic itself.
public static class LedgerReplay
{
    public static Money BalanceAt(
        IEnumerable<JournalEntry> entries,
        AccountId accountId,
        Commodity commodity,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime)
    {
        var balance = Money.Zero(commodity);

        foreach (var entry in ApplicableEntries(entries, asOfValidTime, asOfDecisionTime))
            foreach (var posting in entry.Postings)
            {
                if (posting.AccountId != accountId || posting.Money.Commodity != commodity)
                    continue;

                balance = Apply(balance, posting);
            }

        return balance;
    }

    public static IReadOnlyDictionary<(AccountId AccountId, Commodity Commodity), Money> ReplayBalances(
        IEnumerable<JournalEntry> entries,
        ValidTime asOfValidTime,
        DecisionTime asOfDecisionTime)
    {
        var balances = new Dictionary<(AccountId, Commodity), Money>();

        foreach (var entry in ApplicableEntries(entries, asOfValidTime, asOfDecisionTime))
            foreach (var posting in entry.Postings)
            {
                var key = (posting.AccountId, posting.Money.Commodity);
                var current = balances.TryGetValue(key, out var existing) ? existing : Money.Zero(posting.Money.Commodity);
                balances[key] = Apply(current, posting);
            }

        return balances;
    }

    // Debit-positive convention: the raw balance is Σdebits − Σcredits. Asset/Expense accounts read
    // this directly; a Liability/Equity/Income account's "normal" (positive) balance is the negation
    // of this value. Presenting the sign correctly per AccountType is a read-model concern, deferred
    // past this pure replay function.
    private static Money Apply(Money balance, Posting posting) =>
        posting.Direction == PostingDirection.Debit ? balance.Add(posting.Money) : balance.Subtract(posting.Money);

    private static IEnumerable<JournalEntry> ApplicableEntries(
        IEnumerable<JournalEntry> entries, ValidTime asOfValidTime, DecisionTime asOfDecisionTime) =>
        entries
            .Where(e => e.ValidTime <= asOfValidTime && e.DecisionTime <= asOfDecisionTime)
            .OrderBy(e => e.DecisionTime)
            .ThenBy(e => e.Id.Value); // stable tie-break for entries sharing a DecisionTime instant
}
