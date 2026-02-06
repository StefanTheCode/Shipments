using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shipments.Application.Abstraction;
using Shipments.Application.Abstractions;
using Shipments.Application.Messages;
using Shipments.Application.Results;
using Shipments.Domain;

namespace Shipments.Application.UseCases;

public sealed class DocumentProcessingUseCase(
    IShipmentsDbContext db,
    IBlobStorage blobStorage,
    ILogger<DocumentProcessingUseCase> logger)
    : IDocumentProcessingUseCase
{
    private static class EventIds
    {
        public static readonly EventId ProcessingStarted = new(3000, nameof(ProcessingStarted));
        public static readonly EventId ShipmentNotFound = new(3001, nameof(ShipmentNotFound));
        public static readonly EventId IdempotencyHit = new(3002, nameof(IdempotencyHit));
        public static readonly EventId BlobDownloadStarted = new(3003, nameof(BlobDownloadStarted));
        public static readonly EventId BlobDownloadFailed = new(3004, nameof(BlobDownloadFailed));
        public static readonly EventId ShipmentMarkedProcessed = new(3005, nameof(ShipmentMarkedProcessed));
        public static readonly EventId DbSaveFailed = new(3006, nameof(DbSaveFailed));
        public static readonly EventId ProcessingFailed = new(3099, nameof(ProcessingFailed));
    }

    private static readonly Action<ILogger, Guid, string?, Exception?> _logProcessingStarted =
        LoggerMessage.Define<Guid, string?>(
            LogLevel.Information,
            EventIds.ProcessingStarted,
            "Document processing started. ShipmentId={ShipmentId}, CorrelationId={CorrelationId}");

    private static readonly Action<ILogger, Guid, Exception?> _logShipmentNotFound =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            EventIds.ShipmentNotFound,
            "Shipment not found. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, ShipmentStatus, Exception?> _logIdempotencyHit =
        LoggerMessage.Define<Guid, ShipmentStatus>(
            LogLevel.Information,
            EventIds.IdempotencyHit,
            "Idempotency hit: shipment already processed. No-op. ShipmentId={ShipmentId}, Status={Status}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logBlobDownloadStarted =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            EventIds.BlobDownloadStarted,
            "Blob download started. ShipmentId={ShipmentId}, BlobName={BlobName}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logBlobDownloadFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            EventIds.BlobDownloadFailed,
            "Blob download failed. ShipmentId={ShipmentId}, BlobName={BlobName}");

    private static readonly Action<ILogger, Guid, Exception?> _logShipmentMarkedProcessed =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            EventIds.ShipmentMarkedProcessed,
            "Shipment marked as Processed. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, Exception?> _logDbSaveFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            EventIds.DbSaveFailed,
            "Database save failed while marking shipment processed. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, Exception?> _logProcessingFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            EventIds.ProcessingFailed,
            "Document processing failed. ShipmentId={ShipmentId}");

    public async Task<Result> ProcessAsync(DocumentUploadedMessage message, CancellationToken ct)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["ShipmentId"] = message.ShipmentId,
            ["BlobName"] = message.BlobName
        });

        _logProcessingStarted(logger, message.ShipmentId, message.CorrelationId, null);

        Shipment? shipment;
        try
        {
            shipment = await db.Shipments.FirstOrDefaultAsync(x => x.Id == message.ShipmentId, ct);
        }
        catch (Exception ex)
        {
            _logProcessingFailed(logger, message.ShipmentId, ex);
            return Result.Fail("db_error", "Failed to load shipment from database.");
        }

        if (shipment is null)
        {
            _logShipmentNotFound(logger, message.ShipmentId, null);
            return Result.Fail("not_found", $"Shipment '{message.ShipmentId}' not found.");
        }

        if (shipment.Status == ShipmentStatus.Processed)
        {
            _logIdempotencyHit(logger, shipment.Id, shipment.Status, null);
            return Result.Ok();
        }

        _logBlobDownloadStarted(logger, shipment.Id, message.BlobName, null);

        try
        {
            await using var content = await blobStorage.DownloadAsync(message.BlobName, ct);

            //"Processing"
            await Task.Delay(3000);
        }
        catch (Exception ex)
        {
            _logBlobDownloadFailed(logger, shipment.Id, message.BlobName, ex);
            return Result.Fail("blob_download_failed", "Failed to download blob for processing.");
        }

        shipment.MarkProcessed();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logDbSaveFailed(logger, shipment.Id, ex);
            return Result.Fail("db_save_failed", "Failed to persist processed shipment status.");
        }

        _logShipmentMarkedProcessed(logger, shipment.Id, null);
        return Result.Ok();
    }
}