namespace Shipments.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; init; } = true;
    public int DispatchIntervalSeconds { get; init; } = 2;
    public int BatchSize { get; init; } = 50;
    public int LockSeconds { get; init; } = 30;
}
