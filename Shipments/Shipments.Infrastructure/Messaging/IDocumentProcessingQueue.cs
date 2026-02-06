using Shipments.Application.Messages;

namespace Shipments.Infrastructure.Messaging;

public interface IDocumentProcessingQueue
{
    ValueTask EnqueueAsync(DocumentUploadedMessage message, CancellationToken ct);
    IAsyncEnumerable<DocumentUploadedMessage> DequeueAllAsync(CancellationToken ct);
}
