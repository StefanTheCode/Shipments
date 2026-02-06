namespace Shipments.Infrastructure.Configuration;

public sealed class RuntimeOptions
{
    public const string SectionName = "Runtime";
    public bool UseAzure { get; init; }
}
