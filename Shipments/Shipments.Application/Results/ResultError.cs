namespace Shipments.Application.Results;

public sealed record ResultError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Metadata = null
);