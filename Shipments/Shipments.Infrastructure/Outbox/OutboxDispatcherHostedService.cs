using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstraction;
using Shipments.Application.Messages;
using Shipments.Infrastructure.Persistence;
using System.Text.Json;

namespace Shipments.Infrastructure.Outbox;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private static class EventIds
    {
        public static readonly EventId Disabled = new(8000, nameof(Disabled));
        public static readonly EventId Started = new(8001, nameof(Started));
        public static readonly EventId LoopFailed = new(8002, nameof(LoopFailed));

        public static readonly EventId BatchStart = new(8100, nameof(BatchStart));
        public static readonly EventId BatchEmpty = new(8101, nameof(BatchEmpty));
        public static readonly EventId BatchLocked = new(8102, nameof(BatchLocked));
        public static readonly EventId BatchFailed = new(8103, nameof(BatchFailed));

        public static readonly EventId MessageDispatchStart = new(8200, nameof(MessageDispatchStart));
        public static readonly EventId MessageDispatched = new(8201, nameof(MessageDispatched));
        public static readonly EventId MessageDispatchFailed = new(8202, nameof(MessageDispatchFailed));
        public static readonly EventId UnknownMessageType = new(8203, nameof(UnknownMessageType));
        public static readonly EventId PayloadDeserializeFailed = new(8204, nameof(PayloadDeserializeFailed));
    }

    private static readonly Action<ILogger, Exception?> _logDisabled =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.Disabled,
            "Outbox dispatcher disabled.");

    private static readonly Action<ILogger, string, int, int, int, Exception?> _logStarted =
        LoggerMessage.Define<string, int, int, int>(
            LogLevel.Information,
            EventIds.Started,
            "Outbox dispatcher started. InstanceId={InstanceId}, IntervalSeconds={IntervalSeconds}, BatchSize={BatchSize}, LockSeconds={LockSeconds}");

    private static readonly Action<ILogger, Exception?> _logLoopFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            EventIds.LoopFailed,
            "Outbox dispatch loop failed.");

    private static readonly Action<ILogger, string, int, Exception?> _logBatchStart =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            EventIds.BatchStart,
            "Outbox batch dispatch started. InstanceId={InstanceId}, BatchSize={BatchSize}");

    private static readonly Action<ILogger, Exception?> _logBatchEmpty =
        LoggerMessage.Define(
            LogLevel.Debug,
            EventIds.BatchEmpty,
            "Outbox batch empty.");

    private static readonly Action<ILogger, int, DateTimeOffset, Exception?> _logBatchLocked =
        LoggerMessage.Define<int, DateTimeOffset>(
            LogLevel.Debug,
            EventIds.BatchLocked,
            "Outbox batch locked. Count={Count}, LockedUntil={LockedUntil}");

    private static readonly Action<ILogger, Exception?> _logBatchFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            EventIds.BatchFailed,
            "Outbox batch dispatch failed.");

    private static readonly Action<ILogger, Guid, string, int, Exception?> _logMessageDispatchStart =
        LoggerMessage.Define<Guid, string, int>(
            LogLevel.Debug,
            EventIds.MessageDispatchStart,
            "Outbox message dispatch started. OutboxId={OutboxId}, Type={Type}, Attempt={Attempt}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logMessageDispatched =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            EventIds.MessageDispatched,
            "Outbox message dispatched. OutboxId={OutboxId}, Type={Type}");

    private static readonly Action<ILogger, Guid, string, int, Exception?> _logMessageDispatchFailed =
        LoggerMessage.Define<Guid, string, int>(
            LogLevel.Warning,
            EventIds.MessageDispatchFailed,
            "Outbox dispatch failed. OutboxId={OutboxId}, Type={Type}, Attempt={Attempt}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logUnknownMessageType =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            EventIds.UnknownMessageType,
            "Unknown outbox message type. OutboxId={OutboxId}, Type={Type}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logPayloadDeserializeFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            EventIds.PayloadDeserializeFailed,
            "Outbox payload deserialization failed. OutboxId={OutboxId}, Type={Type}");

    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly string _instanceId = Environment.MachineName + ":" + Guid.NewGuid().ToString("N")[..8];

    public OutboxDispatcherHostedService(
        ILogger<OutboxDispatcherHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logDisabled(_logger, null);
            return;
        }

        _logStarted(_logger, _instanceId, _options.DispatchIntervalSeconds, _options.BatchSize, _options.LockSeconds, null);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logLoopFailed(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.DispatchIntervalSeconds), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        _logBatchStart(_logger, _instanceId, _options.BatchSize, null);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var now = DateTimeOffset.UtcNow;
        var lockUntil = now.AddSeconds(_options.LockSeconds);

        List<OutboxMessage> candidates;
        try
        {
            candidates = await db.OutboxMessages
                .Where(x => x.DispatchedAt == null &&
                            (x.LockedUntil == null || x.LockedUntil < now))
                .OrderBy(x => x.OccurredAt)
                .Take(_options.BatchSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logBatchFailed(_logger, ex);
            return;
        }

        if (candidates.Count == 0)
        {
            _logBatchEmpty(_logger, null);
            return;
        }

        foreach (var m in candidates)
        {
            m.LockedUntil = lockUntil;
            m.LockedBy = _instanceId;
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logBatchLocked(_logger, candidates.Count, lockUntil, null);
        }
        catch (Exception ex)
        {
            _logBatchFailed(_logger, ex);
            return;
        }

        foreach (var m in candidates)
        {
            using var __ = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = m.CorrelationId,
                ["OutboxId"] = m.Id,
                ["OutboxType"] = m.Type,
                ["InstanceId"] = _instanceId
            });

            _logMessageDispatchStart(_logger, m.Id, m.Type, m.AttemptCount + 1, null);

            try
            {
                switch (m.Type)
                {
                    case "DocumentUploaded":
                        {
                            var msg = JsonSerializer.Deserialize<DocumentUploadedMessage>(m.Payload);
                            if (msg is null)
                            {
                                _logPayloadDeserializeFailed(_logger, m.Id, m.Type, null);
                                throw new InvalidOperationException("Outbox payload deserialize returned null.");
                            }

                            await publisher.PublishAsync(msg, ct).ConfigureAwait(false);
                            break;
                        }

                    default:
                        _logUnknownMessageType(_logger, m.Id, m.Type, null);
                        throw new InvalidOperationException($"Unknown outbox message type: '{m.Type}'.");
                }

                m.DispatchedAt = DateTimeOffset.UtcNow;
                m.LastError = null;

                _logMessageDispatched(_logger, m.Id, m.Type, null);
            }
            catch (Exception ex)
            {
                m.AttemptCount++;
                m.LastError = ex.Message;

                m.LockedUntil = DateTimeOffset.UtcNow.AddSeconds(3);

                _logMessageDispatchFailed(_logger, m.Id, m.Type, m.AttemptCount, ex);
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logBatchFailed(_logger, ex);
            }
        }
    }
}