using Microsoft.Extensions.Logging;
using Shipments.Application.Abstractions;
using Shipments.Infrastructure.Persistence;

namespace Shipments.Infrastructure.Outbox;

public sealed class EfOutboxWriter : IOutboxWriter
{
    private static class EventIds
    {
        public static readonly EventId EnqueueRequested = new(7000, nameof(EnqueueRequested));
        public static readonly EventId EnqueueSucceeded = new(7001, nameof(EnqueueSucceeded));
        public static readonly EventId EnqueueFailed = new(7002, nameof(EnqueueFailed));
    }

    private static readonly Action<ILogger, string, string?, Exception?> _logEnqueueRequested =
        LoggerMessage.Define<string, string?>(
            LogLevel.Debug,
            EventIds.EnqueueRequested,
            "Outbox enqueue requested. Type={Type}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logEnqueueSucceeded =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Debug,
            EventIds.EnqueueSucceeded,
            "Outbox message enqueued. OutboxId={OutboxId}, Type={Type}");

    private static readonly Action<ILogger, string, Exception?> _logEnqueueFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.EnqueueFailed,
            "Outbox enqueue failed. Type={Type}");

    private readonly ShipmentsDbContext _db;
    private readonly ILogger<EfOutboxWriter> _logger;

    public EfOutboxWriter(
        ShipmentsDbContext db,
        ILogger<EfOutboxWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task EnqueueAsync(string type, string payload, string correlationId, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["OutboxType"] = type
        });

        _logEnqueueRequested(_logger, type, correlationId, null);

        try
        {
            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                Type = type,
                Payload = payload,
                CorrelationId = correlationId
            };

            _db.OutboxMessages.Add(message);

            _logEnqueueSucceeded(_logger, message.Id, type, null);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logEnqueueFailed(_logger, type, ex);
            throw;
        }
    }
}
