using System.Collections.Immutable;
using Atlas.Kernel;

namespace Atlas.Modules.Ledger.Contracts;

// The published event for R03 (Ledger -> Position & Valuation) in docs/02-domain/03-context-map.md.
// Deliberately minimal: no consumer module exists yet to validate the shape against, so this covers
// exactly what Slice 2 can prove — an entry was posted, with enough detail to replay a balance
// change. Kind/Direction are strings, not the Domain enums, because Contracts may depend only on
// Atlas.Kernel (the module rule) — Domain's JournalEntryKind/PostingDirection are out of reach here
// by construction, not by oversight.
public sealed record JournalEntryPosted(
    Guid EntryId,
    TenantId TenantId,
    ValidTime ValidTime,
    DecisionTime DecisionTime,
    string Kind, // "Original" | "Reversal" | "Replacement"
    Guid? CorrectsEntryId,
    string SourceId,
    ImmutableArray<PostedPosting> Postings);

public sealed record PostedPosting(Guid AccountId, Money Money, string Direction); // "Debit" | "Credit"
