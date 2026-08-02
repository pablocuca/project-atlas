namespace Atlas.Modules.Ingestion.Domain;

public sealed record ParsedRow(int RowNumber, string RawLine, DateTimeOffset Date, string Description, decimal Amount);

// Stage 2 (PARSE) failures are recorded per row, never fatal to the batch
// (docs/03-architecture/05-ingestion-and-integration.md §3) — this is what makes that true.
public sealed record ParseFailure(int RowNumber, string RawLine, string Reason);
