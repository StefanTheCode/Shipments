namespace Shipments.Infrastructure.Processing;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public int SimulatedDelayMs { get; init; } = 1500;
}
