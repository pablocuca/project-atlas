using Azure.Storage.Blobs;

namespace Atlas.Modules.Ingestion.Infrastructure;

// Azure.Storage.Blobs defaults to the newest wire API version it knows, which the pinned local/CI
// Azurite image (3.34.0, docker-compose.yml) predates and rejects outright ("API version ... is not
// supported"). Every BlobServiceClient constructed against that image — Atlas.Host's IngestionModule
// and the integration test fixture alike — needs this pin; kept in one place so the fix doesn't
// drift out of sync between the two callers.
public static class AzuriteBlobClientOptions
{
    public static BlobClientOptions Create() => new(BlobClientOptions.ServiceVersion.V2024_08_04);
}
