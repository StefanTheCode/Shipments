namespace Shipments.Application.Abstractions;

public interface IOutboxWriter
{
    Task EnqueueAsync(
        string type,
        string payload,
        string correlationId,
        CancellationToken ct);
}