namespace Atlas.Modules.Ingestion.Domain;

// The unmodified content of a source file, archived before any interpretation
// (docs/03-architecture/05-ingestion-and-integration.md §3, stage 1 — "the most important stage").
// A thin wrapper rather than a bare string so parsing/archiving code signatures say what they mean
// (CS-7: no primitive obsession).
public sealed record RawPayload(string Content);
