namespace Shipments.Application.Messages;

public sealed class DocumentUploadedMessage
{
    public Guid ShipmentId { get; init; }
    public string BlobName { get; init; } = default!;
    public string CorrelationId { get; init; } = default!;
}
