using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstractions;
using Shipments.Infrastructure.Messaging;

namespace Shipments.Infrastructure.Processing;

public sealed class DocumentProcessingBackgroundService(
    ILogger<DocumentProcessingBackgroundService> logger,
    IDocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<DocumentProcessingOptions> options)
    : BackgroundService
{
    private static class EventIds
    {
        public static readonly EventId Started = new(9000, nameof(Started));
        public static readonly EventId DequeueLoopFailed = new(9001, nameof(DequeueLoopFailed));

        public static readonly EventId MessageReceived = new(9100, nameof(MessageReceived));
        public static readonly EventId SimulatedDelay = new(9101, nameof(SimulatedDelay));
        public static readonly EventId ProcessingSucceeded = new(9102, nameof(ProcessingSucceeded));
        public static readonly EventId ProcessingFailed = new(9103, nameof(ProcessingFailed));
        public static readonly EventId UseCaseFailed = new(9104, nameof(UseCaseFailed));
    }

    private static readonly Action<ILogger, Exception?> _logStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.Started,
            "Local DocumentProcessingBackgroundService started.");

    private static readonly Action<ILogger, Exception?> _logDequeueLoopFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            EventIds.DequeueLoopFailed,
            "Document processing dequeue loop failed.");

    private static readonly Action<ILogger, Guid, string?, Exception?> _logMessageReceived =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Debug,
            EventIds.MessageReceived,
            "Document processing message received. ShipmentId={ShipmentId}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, int, Exception?> _logSimulatedDelay =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            EventIds.SimulatedDelay,
            "Simulated processing delay applied. DelayMs={DelayMs}");

    private static readonly Action<ILogger, Exception?> _logProcessingSucceeded =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.ProcessingSucceeded,
            "Processing succeeded.");

    private static readonly Action<ILogger, string, string, Exception?> _logProcessingFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            EventIds.ProcessingFailed,
            "Processing failed. Code={Code}, Message={Message}");

    private static readonly Action<ILogger, Exception?> _logUseCaseFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            EventIds.UseCaseFailed,
            "Document processing use case threw an exception.");

    private readonly DocumentProcessingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logStarted(logger, null);

        try
        {
            await foreach (var msg in queue.DequeueAllAsync(stoppingToken))
            {
                _logMessageReceived(logger, msg.ShipmentId, msg.CorrelationId, null);

                using var scope = scopeFactory.CreateScope();

                var useCase = scope.ServiceProvider.GetRequiredService<IDocumentProcessingUseCase>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentProcessingBackgroundService>>();

                using var __ = scopedLogger.BeginScope(new Dictionary<string, object?>
                {
                    ["CorrelationId"] = msg.CorrelationId,
                    ["ShipmentId"] = msg.ShipmentId,
                    ["BlobName"] = msg.BlobName
                });

                try
                {
                    if (_options.SimulatedDelayMs > 0)
                    {
                        _logSimulatedDelay(scopedLogger, _options.SimulatedDelayMs, null);
                        await Task.Delay(_options.SimulatedDelayMs, stoppingToken).ConfigureAwait(false);
                    }

                    var result = await useCase.ProcessAsync(msg, stoppingToken).ConfigureAwait(false);

                    if (!result.IsSuccess)
                    {
                        _logProcessingFailed(scopedLogger, result.Error.Code, result.Error.Message, null);
                    }
                    else
                    {
                        _logProcessingSucceeded(scopedLogger, null);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logUseCaseFailed(scopedLogger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logDequeueLoopFailed(logger, ex);
        }
    }
}