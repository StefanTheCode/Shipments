namespace Shipments.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public string? ConnectionString { get; init; }
    public string ContainerName { get; init; } = "shipments-documents";
    public string LocalRootPath { get; init; } = "blobdata";
}
