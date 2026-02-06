using Shipments.Application.Messages;
using Shipments.Application.Results;

namespace Shipments.Application.Abstractions;

public interface IDocumentProcessingUseCase
{
    Task<Result> ProcessAsync(DocumentUploadedMessage message, CancellationToken ct);
}
