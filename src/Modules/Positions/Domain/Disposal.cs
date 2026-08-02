using Atlas.Kernel;

namespace Atlas.Modules.Positions.Domain;

// Disposal := (quantity, proceeds, disposedAt, entryId) (docs/02-domain/04-domain-model.md §3).
// Tracked at the Position level rather than nested under a specific Lot: under custo médio (INV-043)
// a disposal is settled against the Position's running weighted-average cost, not against any one
// Lot's own history, so there is no specific Lot for a Disposal to belong to (Decision 0010).
public sealed record Disposal(decimal Quantity, Money Proceeds, DateTimeOffset DisposedAt, Guid SourceEntryId);
