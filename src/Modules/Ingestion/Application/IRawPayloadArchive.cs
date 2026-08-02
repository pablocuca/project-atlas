using Atlas.Modules.Ingestion.Domain;

namespace Atlas.Modules.Ingestion.Application;

// Stage 1, CAPTURE (docs/03-architecture/05-ingestion-and-integration.md §3) — a port; implemented
// against Azurite/Blob storage in Ingestion.Infrastructure (data-strategy.md §1 assigns raw payloads
// to Blob, not Postgres). Returns a reference to where the payload was archived.
public interface IRawPayloadArchive
{
    Task<string> ArchiveAsync(Guid tenantId, string sourceId, RawPayload payload, CancellationToken cancellationToken);
}
