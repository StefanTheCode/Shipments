using Microsoft.Extensions.Options;

namespace Shipments.Infrastructure.Storage;

public sealed class ValidateBlobOptions : IValidateOptions<BlobStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, BlobStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("BlobStorage:ConnectionString is required when Runtime:UseAzure=true.");
        }

        if (string.IsNullOrWhiteSpace(options.ContainerName))
        {
            return ValidateOptionsResult.Fail("BlobStorage:ContainerName is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
