namespace Shipments.Application.Dtos;

public sealed class CreateShipmentRequest
{
    public string ReferenceNumber { get; init; } = default!;
    public string Sender { get; init; } = default!;
    public string Recipient { get; init; } = default!;
}
