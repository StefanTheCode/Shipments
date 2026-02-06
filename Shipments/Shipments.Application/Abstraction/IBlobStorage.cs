namespace Shipments.Application.Abstraction;

public interface IBlobStorage
{
    Task UploadAsync(
        Stream content,
        string blobName,
        string contentType,
        CancellationToken ct);

    Task<Stream> DownloadAsync(string blobName, CancellationToken ct);
}