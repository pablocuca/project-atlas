using System.Collections.Immutable;
using Atlas.Kernel;
using Atlas.Modules.Ledger.Contracts;
using Atlas.Modules.Ledger.Domain;
using Atlas.Modules.Ledger.Domain.Entries;

namespace Atlas.Modules.Ledger.Application;

// The IPostJournalEntry implementation — a thin translation from the published-language command
// shape to Domain types and back, wrapping the existing PostJournalEntryHandler rather than
// duplicating its logic.
public sealed class PostJournalEntryPort(PostJournalEntryHandler handler) : IPostJournalEntry
{
    public async Task<Result<JournalEntryPosted>> PostAsync(PostJournalEntryCommand command, CancellationToken cancellationToken)
    {
        var postings = ToPostings(command.Postings);
        if (postings.IsFailure)
            return Result.Fail<JournalEntryPosted>(postings.Error);

        var result = await handler.HandleAsync(
            command.TenantId, command.ValidTime, command.DecisionTime, command.CurrentTradingDayClose,
            command.Description, command.SourceId, command.IdempotencyKey, postings.Value, cancellationToken);

        return result.IsSuccess
            ? Result.Ok(result.Value.PublishedEvent)
            : Result.Fail<JournalEntryPosted>(result.Error);
    }

    private static Result<ImmutableArray<Posting>> ToPostings(ImmutableArray<PostingCommand> postings)
    {
        var builder = ImmutableArray.CreateBuilder<Posting>(postings.Length);
        foreach (var posting in postings)
        {
            var created = Posting.Create(new AccountId(posting.AccountId), posting.Money, Enum.Parse<PostingDirection>(posting.Direction));
            if (created.IsFailure)
                return Result.Fail<ImmutableArray<Posting>>(created.Error);

            builder.Add(created.Value);
        }

        return Result.Ok(builder.ToImmutable());
    }
}
