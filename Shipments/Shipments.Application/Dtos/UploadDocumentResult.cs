namespace Shipments.Application.Dtos;

public sealed class UploadDocumentResult
{
    public Guid DocumentId { get; init; }
    public string BlobName { get; init; } = default!;
}
