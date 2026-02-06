using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shipments.Application.Abstraction;

namespace Shipments.Infrastructure.Storage;

public sealed class LocalFileBlobStorage : IBlobStorage
{
    private static class EventIds
    {
        public static readonly EventId RootEnsured = new(12000, nameof(RootEnsured));

        public static readonly EventId UploadStarted = new(12100, nameof(UploadStarted));
        public static readonly EventId UploadSucceeded = new(12101, nameof(UploadSucceeded));
        public static readonly EventId UploadFailed = new(12102, nameof(UploadFailed));

        public static readonly EventId DownloadStarted = new(12200, nameof(DownloadStarted));
        public static readonly EventId DownloadSucceeded = new(12201, nameof(DownloadSucceeded));
        public static readonly EventId DownloadFailed = new(12202, nameof(DownloadFailed));
    }

    private static readonly Action<ILogger, string, Exception?> _logRootEnsured =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.RootEnsured,
            "Local blob storage root ensured. RootPath={RootPath}");

    private static readonly Action<ILogger, string, Exception?> _logUploadStarted =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.UploadStarted,
            "Local blob upload started. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logUploadSucceeded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.UploadSucceeded,
            "Local blob upload succeeded. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logUploadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.UploadFailed,
            "Local blob upload failed. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadStarted =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DownloadStarted,
            "Local blob download started. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadSucceeded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DownloadSucceeded,
            "Local blob download succeeded. BlobName={BlobName}");

    private static readonly Action<ILogger, string, Exception?> _logDownloadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.DownloadFailed,
            "Local blob download failed. BlobName={BlobName}");

    private readonly string _root;
    private readonly ILogger<LocalFileBlobStorage> _logger;

    public LocalFileBlobStorage(
        IOptions<BlobStorageOptions> options,
        ILogger<LocalFileBlobStorage> logger)
    {
        _logger = logger;

        _root = Path.GetFullPath(options.Value.LocalRootPath);
        Directory.CreateDirectory(_root);

        _logRootEnsured(_logger, _root, null);
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
            var safe = blobName.Replace('\\', '/').TrimStart('/');
            var fullPath = Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fs = File.Create(fullPath);
            await content.CopyToAsync(fs, ct).ConfigureAwait(false);

            _logUploadSucceeded(_logger, blobName, null);
        }
        catch (Exception ex)
        {
            _logUploadFailed(_logger, blobName, ex);
            throw;
        }
    }

    public Task<Stream> DownloadAsync(string blobName, CancellationToken ct)
    {
        _logDownloadStarted(_logger, blobName, null);

        try
        {
            var safe = blobName.Replace('\\', '/').TrimStart('/');
            var fullPath = Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));

            Stream stream = File.OpenRead(fullPath);

            _logDownloadSucceeded(_logger, blobName, null);
            return Task.FromResult(stream);
        }
        catch (Exception ex)
        {
            _logDownloadFailed(_logger, blobName, ex);
            throw;
        }
    }
}
