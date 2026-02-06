namespace Shipments.Api.Observability;

public sealed class CorrelationOptions
{
    public const string SectionName = "Correlation";
    public string HeaderName { get; init; } = "X-Correlation-Id";
}