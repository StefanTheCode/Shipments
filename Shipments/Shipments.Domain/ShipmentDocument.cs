namespace Shipments.Domain;

public class ShipmentDocument
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ShipmentId { get; private set; }
    public Shipment Shipment { get; private set; } = default!;

    public string BlobName { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = "application/octet-stream";
    public long Size { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Optional but "senior": idempotency / dedupe
    public string? ContentSha256 { get; private set; }

    private ShipmentDocument() { } // EF

    public ShipmentDocument(Guid shipmentId, string blobName, string fileName, string contentType, long size, string? contentSha256 = null)
    {
        ShipmentId = shipmentId;
        BlobName = blobName?.Trim() ?? throw new ArgumentNullException(nameof(blobName));
        FileName = fileName?.Trim() ?? throw new ArgumentNullException(nameof(fileName));
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        Size = size;
        ContentSha256 = contentSha256;
    }
}
