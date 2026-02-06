namespace Shipments.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";
    public string? ConnectionString { get; init; }
    public string QueueName { get; init; } = "documents-to-process";
}
