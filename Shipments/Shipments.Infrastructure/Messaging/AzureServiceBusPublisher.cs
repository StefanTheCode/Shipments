using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstraction;
using Shipments.Application.Messages;
using System.Text.Json;

namespace Shipments.Infrastructure.Messaging;

public sealed class AzureServiceBusPublisher : IMessagePublisher
{
    private static class EventIds
    {
        public static readonly EventId PublishStarted = new(4000, nameof(PublishStarted));
        public static readonly EventId PublishSucceeded = new(4001, nameof(PublishSucceeded));
        public static readonly EventId PublishFailed = new(4002, nameof(PublishFailed));
    }

    private static readonly Action<ILogger, Guid, string?, Exception?> _logPublishStarted =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Information,
            EventIds.PublishStarted,
            "Publishing message to Azure Service Bus. ShipmentId={ShipmentId}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, Guid, Exception?> _logPublishSucceeded =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            EventIds.PublishSucceeded,
            "Message successfully published to Azure Service Bus. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, Exception?> _logPublishFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            EventIds.PublishFailed,
            "Failed to publish message to Azure Service Bus. ShipmentId={ShipmentId}");

    private readonly ServiceBusSender _sender;
    private readonly ILogger<AzureServiceBusPublisher> _logger;

    public AzureServiceBusPublisher(
        IOptions<ServiceBusOptions> options,
        ILogger<AzureServiceBusPublisher> logger)
    {
        _logger = logger;

        var busOptions = options.Value;

        var client = new ServiceBusClient(busOptions.ConnectionString);
        _sender = client.CreateSender(busOptions.QueueName);
    }

    public async Task PublishAsync(DocumentUploadedMessage message, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["ShipmentId"] = message.ShipmentId,
            ["BlobName"] = message.BlobName
        });

        _logPublishStarted(_logger, message.ShipmentId, message.CorrelationId, null);

        try
        {
            var json = JsonSerializer.Serialize(message);

            var sbMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                Subject = "ShipmentDocumentUploaded",
                CorrelationId = message.CorrelationId
            };

            sbMessage.ApplicationProperties["shipmentId"] = message.ShipmentId.ToString();
            sbMessage.ApplicationProperties["blobName"] = message.BlobName;

            await _sender.SendMessageAsync(sbMessage, ct);

            _logPublishSucceeded(_logger, message.ShipmentId, null);
        }
        catch (Exception ex)
        {
            _logPublishFailed(_logger, message.ShipmentId, ex);
            throw;
        }
    }
}
