namespace Atlas.Modules.Ingestion.Application;

public sealed record DuplicateCandidateRecord(
    Guid CandidateId, Guid TenantId, Guid NewEntryId, Guid ExistingEntryId, double Similarity, DateTimeOffset DetectedAt);

// FR-110: queued for human resolution, never auto-merged. No resolution UI exists yet (mirrors
// Decision 0006's reasoning for the CONFIRM stage) — this slice's job is detecting and recording
// the candidate; a review surface is a later, separate concern.
public interface IDuplicateCandidateRepository
{
    Task RecordAsync(DuplicateCandidateRecord record, CancellationToken cancellationToken);
}
