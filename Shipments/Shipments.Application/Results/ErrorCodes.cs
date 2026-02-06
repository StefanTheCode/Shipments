namespace Shipments.Application.Results;

public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ExternalDependency = "external_dependency";
    public const string Unexpected = "unexpected";
}