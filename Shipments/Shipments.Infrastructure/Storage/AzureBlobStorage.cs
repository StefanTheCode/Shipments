using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstraction;

namespace Shipments.Infrastructure.Storage;

public sealed class AzureBlobStorage : IBlobStorage
{
    private static class EventIds
    {
        public static readonly EventId ContainerEnsured = new(11000, nameof(ContainerEnsured));

        public static readonly EventId UploadStarted = new(11100, nameof(UploadStarted));
        public static readonly EventId UploadSucceeded = new(11101, nameof(UploadSucceeded));
        public static readonly EventId UploadFailed = new(11102, nameof(UploadFailed));

        public static readonly EventId DownloadStarted = new(11200, nameof(DownloadStarted));
        public static readonly EventId DownloadSucceeded = new(11201, nameof(DownloadSucceeded));
        public static readonly EventId DownloadFailed = new(11202, nameof(DownloadFailed));
    }

    private static readonly Action<ILogger, string, Exception?> _logContainerEnsured =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.ContainerEnsured,
            "Azure Blob container ensured. Container={ContainerName}");

    private static readonly Action<ILogger, string, Exception?> _logUploadStarted =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.UploadStarted,
            "Blob upload started. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logUploadSucceeded =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.UploadSucceeded,
            "Blob upload succeeded. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logUploadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.UploadFailed,
            "Blob upload failed. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadStarted =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DownloadStarted,
            "Blob download started. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadSucceeded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DownloadSucceeded,
            "Blob download succeeded. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.DownloadFailed,
            "Blob download failed. BlobName={BlobName}");

    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobStorage> _logger;

    public AzureBlobStorage(
        IOptions<BlobStorageOptions> options,
        ILogger<AzureBlobStorage> logger)
    {
        _logger = logger;

        var o = options.Value;

        var serviceClient = new BlobServiceClient(o.ConnectionString);

        _container = serviceClient.GetBlobContainerClient(o.ContainerName);
        _container.CreateIfNotExists(PublicAccessType.None);

        _logContainerEnsured(_logger, o.ContainerName, null);
    }

    public async Task UploadAsync(
        Stream content,
        string blobName,
        string contentType,
        CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        _logUploadStarted(_logger, blobName, null);

        try
        {
            var blobClient = _container.GetBlobClient(blobName);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            await blobClient.UploadAsync(content, options, ct).ConfigureAwait(false);

            _logUploadSucceeded(_logger, blobName, null);
        }
        catch (Exception ex)
        {
            _logUploadFailed(_logger, blobName, ex);
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(string blobName, CancellationToken ct)
    {
        _logDownloadStarted(_logger, blobName, null);

        try
        {
            var blobClient = _container.GetBlobClient(blobName);

            Response<BlobDownloadStreamingResult> response =
                await blobClient.DownloadStreamingAsync(cancellationToken: ct).ConfigureAwait(false);

            _logDownloadSucceeded(_logger, blobName, null);

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logDownloadFailed(_logger, blobName, ex);
            throw;
        }
    }
}
