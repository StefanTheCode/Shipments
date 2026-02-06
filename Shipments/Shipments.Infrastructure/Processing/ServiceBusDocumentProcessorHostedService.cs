using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstractions;
using Shipments.Application.Messages;
using Shipments.Infrastructure.Messaging;

namespace Shipments.Infrastructure.Processing;

public sealed class ServiceBusDocumentProcessorHostedService : BackgroundService
{
    private static class EventIds
    {
        public static readonly EventId ProcessorStarting = new(10000, nameof(ProcessorStarting));
        public static readonly EventId ProcessorStarted = new(10001, nameof(ProcessorStarted));
        public static readonly EventId ProcessorStopping = new(10002, nameof(ProcessorStopping));
        public static readonly EventId ProcessorStopped = new(10003, nameof(ProcessorStopped));

        public static readonly EventId MessageReceived = new(10100, nameof(MessageReceived));
        public static readonly EventId DeserializeFailed = new(10101, nameof(DeserializeFailed));
        public static readonly EventId DeadLettered = new(10102, nameof(DeadLettered));
        public static readonly EventId Completed = new(10103, nameof(Completed));
        public static readonly EventId ProcessingFailed = new(10104, nameof(ProcessingFailed));

        public static readonly EventId ServiceBusError = new(10200, nameof(ServiceBusError));
    }

    private static readonly Action<ILogger, Exception?> _logProcessorStarting =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.ProcessorStarting,
            "ServiceBus processor starting...");

    private static readonly Action<ILogger, Exception?> _logProcessorStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.ProcessorStarted,
            "ServiceBus processor started.");

    private static readonly Action<ILogger, Exception?> _logProcessorStopping =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.ProcessorStopping,
            "ServiceBus processor stopping...");

    private static readonly Action<ILogger, Exception?> _logProcessorStopped =
        LoggerMessage.Define(
            LogLevel.Information,
            EventIds.ProcessorStopped,
            "ServiceBus processor stopped.");

    private static readonly Action<ILogger, string, int, Exception?> _logMessageReceived =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            EventIds.MessageReceived,
            "SB message received. MessageId={MessageId}, DeliveryCount={DeliveryCount}");

    private static readonly Action<ILogger, string, Exception?> _logDeserializeFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.DeserializeFailed,
            "SB message deserialization failed. MessageId={MessageId}");

    private static readonly Action<ILogger, string, string, string, Exception?> _logDeadLettered =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            EventIds.DeadLettered,
            "SB message dead-lettered. MessageId={MessageId}, Reason={Reason}, Description={Description}");

    private static readonly Action<ILogger, string, Exception?> _logCompleted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.Completed,
            "SB message completed. MessageId={MessageId}");

    private static readonly Action<ILogger, string, int, Exception?> _logProcessingFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            EventIds.ProcessingFailed,
            "SB processing failed. MessageId={MessageId}, DeliveryCount={DeliveryCount}");

    private static readonly Action<ILogger, string, ServiceBusErrorSource, Exception?> _logServiceBusError =
        LoggerMessage.Define<string, ServiceBusErrorSource>(
            LogLevel.Error,
            EventIds.ServiceBusError,
            "ServiceBus error. Entity={EntityPath}, Source={ErrorSource}");

    private readonly ILogger<ServiceBusDocumentProcessorHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusProcessor _processor;

    public ServiceBusDocumentProcessorHostedService(
        ILogger<ServiceBusDocumentProcessorHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<ServiceBusOptions> sbOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var o = sbOptions.Value;
        var client = new ServiceBusClient(o.ConnectionString);

        _processor = client.CreateProcessor(o.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        _logProcessorStarting(_logger, null);

        await _processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);

        _logProcessorStarted(_logger, null);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        _logMessageReceived(_logger, args.Message.MessageId, args.Message.DeliveryCount, null);

        DocumentUploadedMessage? msg = null;

        try
        {
            msg = JsonSerializer.Deserialize<DocumentUploadedMessage>(args.Message.Body.ToArray());

            if (msg is null)
            {
                _logDeserializeFailed(_logger, args.Message.MessageId, null);

                await args.DeadLetterMessageAsync(
                        args.Message,
                        deadLetterReason: "InvalidMessage",
                        deadLetterErrorDescription: "Body deserialization returned null.")
                    .ConfigureAwait(false);

                _logDeadLettered(_logger, args.Message.MessageId, "InvalidMessage", "Body deserialization returned null.", null);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IDocumentProcessingUseCase>();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceBusDocumentProcessorHostedService>>();

            using var __ = scopedLogger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["ShipmentId"] = msg.ShipmentId,
                ["BlobName"] = msg.BlobName,
                ["MessageId"] = args.Message.MessageId,
                ["DeliveryCount"] = args.Message.DeliveryCount
            });

            var result = await useCase.ProcessAsync(msg, args.CancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                if (result.Error.Code is "not_found" or "invalid")
                {
                    await args.DeadLetterMessageAsync(args.Message, result.Error.Code, result.Error.Message)
                        .ConfigureAwait(false);

                    _logDeadLettered(_logger, args.Message.MessageId, result.Error.Code, result.Error.Message, null);
                    return;
                }

                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }

            await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            _logCompleted(_logger, args.Message.MessageId, null);
        }
        catch (Exception ex)
        {
            _logProcessingFailed(_logger, args.Message.MessageId, args.Message.DeliveryCount, ex);

            throw;
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logServiceBusError(_logger, args.EntityPath, args.ErrorSource, args.Exception);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logProcessorStopping(_logger, null);

        await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
        await _processor.DisposeAsync().ConfigureAwait(false);

        _logProcessorStopped(_logger, null);

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
