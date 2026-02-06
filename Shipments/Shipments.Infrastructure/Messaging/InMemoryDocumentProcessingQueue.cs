using Microsoft.Extensions.Logging;
using Shipments.Application.Messages;
using System.Threading.Channels;

namespace Shipments.Infrastructure.Messaging;

public sealed class InMemoryDocumentProcessingQueue : IDocumentProcessingQueue
{
    private static class EventIds
    {
        public static readonly EventId EnqueueRequested = new(5000, nameof(EnqueueRequested));
        public static readonly EventId EnqueueSucceeded = new(5001, nameof(EnqueueSucceeded));
        public static readonly EventId EnqueueFailed = new(5002, nameof(EnqueueFailed));
        public static readonly EventId DequeueStarted = new(5003, nameof(DequeueStarted));
    }

    private static readonly Action<ILogger, Guid, string?, Exception?> _logEnqueueRequested =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Debug,
            EventIds.EnqueueRequested,
            "Queue enqueue requested. ShipmentId={ShipmentId}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, Guid, Exception?> _logEnqueueSucceeded =
        LoggerMessage.Define<Guid>(
            LogLevel.Debug,
            EventIds.EnqueueSucceeded,
            "Queue enqueue succeeded. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, Exception?> _logEnqueueFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            EventIds.EnqueueFailed,
            "Queue enqueue failed. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Exception?> _logDequeueStarted =
        LoggerMessage.Define(
            LogLevel.Debug,
            EventIds.DequeueStarted,
            "Queue dequeue started.");

    private readonly Channel<DocumentUploadedMessage> _channel =
        Channel.CreateUnbounded<DocumentUploadedMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ILogger<InMemoryDocumentProcessingQueue> _logger;

    public InMemoryDocumentProcessingQueue(ILogger<InMemoryDocumentProcessingQueue> logger)
    {
        _logger = logger;
    }

    public ValueTask EnqueueAsync(DocumentUploadedMessage message, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["ShipmentId"] = message.ShipmentId
        });

        _logEnqueueRequested(_logger, message.ShipmentId, message.CorrelationId, null);

        try
        {
            var vt = _channel.Writer.WriteAsync(message, ct);

            if (vt.IsCompletedSuccessfully)
            {
                _logEnqueueSucceeded(_logger, message.ShipmentId, null);
                return vt;
            }

            return AwaitAndLogAsync(vt, message.ShipmentId);

            async ValueTask AwaitAndLogAsync(ValueTask inner, Guid shipmentId)
            {
                try
                {
                    await inner.ConfigureAwait(false);
                    _logEnqueueSucceeded(_logger, shipmentId, null);
                }
                catch (Exception ex)
                {
                    _logEnqueueFailed(_logger, shipmentId, ex);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logEnqueueFailed(_logger, message.ShipmentId, ex);
            throw;
        }
    }

    public IAsyncEnumerable<DocumentUploadedMessage> DequeueAllAsync(CancellationToken ct)
    {
        _logDequeueStarted(_logger, null);
        return _channel.Reader.ReadAllAsync(ct);
    }
}
