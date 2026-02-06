namespace Shipments.Application.Abstractions;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}