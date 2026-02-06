namespace Shipments.Application.Dtos;

public sealed class ShipmentDocumentResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = default!;
    public string BlobName { get; init; } = default!;
    public long Size { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}
