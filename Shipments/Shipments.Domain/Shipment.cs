namespace Shipments.Domain;

public class Shipment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string ReferenceNumber { get; private set; } = default!;
    public string Sender { get; private set; } = default!;
    public string Recipient { get; private set; } = default!;

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Created;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    public List<ShipmentDocument> Documents { get; private set; } = new();

    private Shipment() { }

    public Shipment(string referenceNumber, string sender, string recipient)
    {
        ReferenceNumber = referenceNumber?.Trim() ?? throw new ArgumentNullException(nameof(referenceNumber));
        Sender = sender?.Trim() ?? throw new ArgumentNullException(nameof(sender));
        Recipient = recipient?.Trim() ?? throw new ArgumentNullException(nameof(recipient));
    }

    public void MarkDocumentUploaded()
    {
        if (Status < ShipmentStatus.DocumentUploaded)
        {
            Status = ShipmentStatus.DocumentUploaded;
        }
    }

    public void MarkProcessed()
    {
        Status = ShipmentStatus.Processed;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public static Shipment Create(string referenceNumber, string sender, string recipient)
    {
        return new Shipment
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = referenceNumber,
            Sender = sender,
            Recipient = recipient,
            Status = ShipmentStatus.Created
        };
    }

}