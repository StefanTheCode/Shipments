using Shipments.Application.Messages;

namespace Shipments.Application.Abstraction;

public interface IMessagePublisher
{
    Task PublishAsync(DocumentUploadedMessage message, CancellationToken ct);
}
