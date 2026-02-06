using Microsoft.Extensions.Options;

namespace Shipments.Infrastructure.Messaging;

public sealed class ValidateServiceBusOptions : IValidateOptions<ServiceBusOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceBusOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("ServiceBus:ConnectionString is required when Runtime:UseAzure=true.");
        }

        if (string.IsNullOrWhiteSpace(options.QueueName))
        {
            return ValidateOptionsResult.Fail("ServiceBus:QueueName is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
