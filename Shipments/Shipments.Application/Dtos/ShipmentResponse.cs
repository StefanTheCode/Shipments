using Shipments.Domain;

namespace Shipments.Application.Dtos;

public sealed class ShipmentResponse
{
    public Guid Id { get; init; }
    public string ReferenceNumber { get; init; } = default!;
    public string Sender { get; init; } = default!;
    public string Recipient { get; init; } = default!;
    public ShipmentStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }

    public IReadOnlyList<ShipmentDocumentResponse> Documents { get; init; } = [];
}
