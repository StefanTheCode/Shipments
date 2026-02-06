using Shipments.Application.Abstractions;

namespace Shipments.Application.Correlation;

public sealed class DefaultCorrelationContext : ICorrelationContext
{
    public string CorrelationId => Guid.NewGuid().ToString("N");
}