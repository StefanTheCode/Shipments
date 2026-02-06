using Microsoft.Extensions.Logging;
using Shipments.Application.Abstraction;
using Shipments.Application.Messages;

namespace Shipments.Infrastructure.Messaging;

public sealed class InMemoryMessagePublisher : IMessagePublisher
{
    private static class EventIds
    {
        public static readonly EventId PublishStarted = new(6000, nameof(PublishStarted));
        public static readonly EventId PublishSucceeded = new(6001, nameof(PublishSucceeded));
        public static readonly EventId PublishFailed = new(6002, nameof(PublishFailed));
    }

    private static readonly Action<ILogger, Guid, string?, Exception?> _logPublishStarted =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Debug,
            EventIds.PublishStarted,
            "In-memory publish started. ShipmentId={ShipmentId}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, Guid, Exception?> _logPublishSucceeded =
        LoggerMessage.Define<Guid>(
            LogLevel.Debug,
            EventIds.PublishSucceeded,
            "In-memory publish succeeded. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, Exception?> _logPublishFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            EventIds.PublishFailed,
            "In-memory publish failed. ShipmentId={ShipmentId}");

    private readonly IDocumentProcessingQueue _queue;
    private readonly ILogger<InMemoryMessagePublisher> _logger;

    public InMemoryMessagePublisher(
        IDocumentProcessingQueue queue,
        ILogger<InMemoryMessagePublisher> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public async Task PublishAsync(DocumentUploadedMessage message, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["ShipmentId"] = message.ShipmentId
        });

        _logPublishStarted(_logger, message.ShipmentId, message.CorrelationId, null);

        try
        {
            await _queue.EnqueueAsync(message, ct);
            _logPublishSucceeded(_logger, message.ShipmentId, null);
        }
        catch (Exception ex)
        {
            _logPublishFailed(_logger, message.ShipmentId, ex);
            throw;
        }
    }
}
