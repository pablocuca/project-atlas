using System.Text;
using Atlas.Modules.Ingestion.Application;
using Atlas.Modules.Ingestion.Domain;
using Azure.Storage.Blobs;

namespace Atlas.Modules.Ingestion.Infrastructure;

// Stage 1, CAPTURE — the raw payload archived before any parsing (docs/03-architecture/
// 05-ingestion-and-integration.md §3). Blob storage, not Postgres, per data-strategy.md §1.
public sealed class BlobRawPayloadArchive(BlobContainerClient containerClient) : IRawPayloadArchive
{
    public async Task<string> ArchiveAsync(
        Guid tenantId, string sourceId, string extension, RawPayload payload, CancellationToken cancellationToken)
    {
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{tenantId:N}/{sourceId}/{Guid.NewGuid():N}.{extension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload.Content));
        await blobClient.UploadAsync(stream, overwrite: false, cancellationToken);

        return blobName;
    }

    // Not part of IRawPayloadArchive — nothing in this slice's Application layer needs to read a
    // payload back. Exists so tests can prove "the raw payload remains recoverable" directly
    // (docs/01-product/10-user-stories.md, US-010's malformed-row scenario) without a second port.
    public async Task<string> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }
}
