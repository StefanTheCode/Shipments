using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shipments.Application.Abstraction;
using Shipments.Application.Abstractions;
using Shipments.Application.Dtos;
using Shipments.Application.Messages;
using Shipments.Application.Results;
using Shipments.Domain;
using System.Text.Json;

namespace Shipments.Application.Services;

public sealed class ShipmentService : IShipmentService
{
    private static class EventIds
    {
        public static readonly EventId ShipmentCreateRequested = new(1000, nameof(ShipmentCreateRequested));
        public static readonly EventId ShipmentCreateValidationFailed = new(1001, nameof(ShipmentCreateValidationFailed));
        public static readonly EventId ShipmentCreateConflict = new(1002, nameof(ShipmentCreateConflict));
        public static readonly EventId ShipmentCreated = new(1003, nameof(ShipmentCreated));

        public static readonly EventId ShipmentGetNotFound = new(1100, nameof(ShipmentGetNotFound));

        public static readonly EventId DocumentUploadStarted = new(2000, nameof(DocumentUploadStarted));
        public static readonly EventId DocumentUploadValidationFailed = new(2001, nameof(DocumentUploadValidationFailed));
        public static readonly EventId DocumentUploadShipmentNotFound = new(2002, nameof(DocumentUploadShipmentNotFound));
        public static readonly EventId BlobUploadFailed = new(2003, nameof(BlobUploadFailed));
        public static readonly EventId BlobUploaded = new(2004, nameof(BlobUploaded));
        public static readonly EventId ShipmentStatusUpdated = new(2005, nameof(ShipmentStatusUpdated));
        public static readonly EventId OutboxEnqueueFailed = new(2006, nameof(OutboxEnqueueFailed));
        public static readonly EventId OutboxEnqueued = new(2007, nameof(OutboxEnqueued));
    }

    private static readonly Action<ILogger, string?, string?, string?, Exception?> _logShipmentCreateRequested =
    LoggerMessage.Define<string?, string?, string?>(
        LogLevel.Information,
        EventIds.ShipmentCreateRequested,
        "Shipment create requested. ReferenceNumber={ReferenceNumber}, Sender={Sender}, Recipient={Recipient}");

    private static readonly Action<ILogger, string, object?, Exception?> _logShipmentCreateValidationFailed =
        LoggerMessage.Define<string, object?>(
            LogLevel.Warning,
            EventIds.ShipmentCreateValidationFailed,
            "Shipment create validation failed. Code={Code}, Details={Details}");

    private static readonly Action<ILogger, string, Exception?> _logShipmentCreateConflict =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.ShipmentCreateConflict,
            "Shipment create rejected due to conflict. ReferenceNumber={ReferenceNumber}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logShipmentCreated =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            EventIds.ShipmentCreated,
            "Shipment created successfully. ShipmentId={ShipmentId}, ReferenceNumber={ReferenceNumber}");

    private static readonly Action<ILogger, Guid, Exception?> _logShipmentGetNotFound =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            EventIds.ShipmentGetNotFound,
            "Shipment not found. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, string, long, Exception?> _logDocumentUploadStarted =
        LoggerMessage.Define<Guid, string, long>(
            LogLevel.Information,
            EventIds.DocumentUploadStarted,
            "Document upload started. ShipmentId={ShipmentId}, FileName={FileName}, Size={Size}");

    private static readonly Action<ILogger, string, object?, Exception?> _logDocumentUploadValidationFailed =
        LoggerMessage.Define<string, object?>(
            LogLevel.Warning,
            EventIds.DocumentUploadValidationFailed,
            "Document upload validation failed. Code={Code}, Details={Details}");

    private static readonly Action<ILogger, Guid, Exception?> _logDocumentUploadShipmentNotFound =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            EventIds.DocumentUploadShipmentNotFound,
            "Document upload rejected. Shipment not found. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, string, string?, Exception?> _logBlobUploadFailed =
        LoggerMessage.Define<Guid, string, string?>(
            LogLevel.Error,
            EventIds.BlobUploadFailed,
            "Blob upload failed. ShipmentId={ShipmentId}, BlobName={BlobName}, ContentType={ContentType}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logBlobUploaded =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            EventIds.BlobUploaded,
            "Document uploaded to storage. ShipmentId={ShipmentId}, BlobName={BlobName}");

    private static readonly Action<ILogger, Guid, Exception?> _logShipmentStatusUpdated =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            EventIds.ShipmentStatusUpdated,
            "Shipment status updated to DocumentUploaded. ShipmentId={ShipmentId}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logOutboxEnqueued =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            EventIds.OutboxEnqueued,
            "Outbox message created. Type=DocumentUploaded, ShipmentId={ShipmentId}, BlobName={BlobName}");

    private static readonly Action<ILogger, Guid, string, Exception?> _logOutboxEnqueueFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            EventIds.OutboxEnqueueFailed,
            "Outbox enqueue failed after successful upload. ShipmentId={ShipmentId}, BlobName={BlobName}");

    private readonly IShipmentsDbContext _db;
    private readonly IBlobStorage _blob;

    private readonly ICorrelationContext _correlation;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<IShipmentService> _logger;

    public ShipmentService(
        IShipmentsDbContext db,
        IBlobStorage blob,
        ICorrelationContext correlation,
        IOutboxWriter outboxWriter,
        ILogger<IShipmentService> logger)
    {
        _db = db;
        _blob = blob;
        _correlation = correlation;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<Result<Guid>> CreateAsync(CreateShipmentRequest request, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = _correlation.CorrelationId
        });

        var reference = request.ReferenceNumber?.Trim();
        var sender = request.Sender?.Trim();
        var recipient = request.Recipient?.Trim();

        _logShipmentCreateRequested(_logger, reference, sender, recipient, null);

        if (string.IsNullOrWhiteSpace(reference))
        {
            _logShipmentCreateValidationFailed(_logger, ErrorCodes.Validation, new { field = "ReferenceNumber", reason = "required" }, null);
            return Result<Guid>.Fail(ErrorCodes.Validation, "ReferenceNumber is required.");
        }

        if (reference.Length > 64)
        {
            _logShipmentCreateValidationFailed(_logger, ErrorCodes.Validation, new { field = "ReferenceNumber", reason = "maxLength", max = 64, actual = reference.Length }, null);
            return Result<Guid>.Fail(ErrorCodes.Validation, "ReferenceNumber max length is 64.");
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            _logShipmentCreateValidationFailed(_logger, ErrorCodes.Validation, new { field = "Sender", reason = "required" }, null);
            return Result<Guid>.Fail(ErrorCodes.Validation, "Sender is required.");
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logShipmentCreateValidationFailed(_logger, ErrorCodes.Validation, new { field = "Recipient", reason = "required" }, null);
            return Result<Guid>.Fail(ErrorCodes.Validation, "Recipient is required.");
        }

        var exists = await _db.Shipments.AnyAsync(x => x.ReferenceNumber == reference, ct);
        if (exists)
        {
            _logShipmentCreateConflict(_logger, reference, null);

            return Result<Guid>.Fail(
                ErrorCodes.Conflict,
                $"Shipment with reference '{reference}' already exists.",
                new Dictionary<string, object?> { ["referenceNumber"] = reference });
        }

        var shipment = new Shipment(reference, sender, recipient);

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);

        _logShipmentCreated(_logger, shipment.Id, shipment.ReferenceNumber, null);

        return Result<Guid>.Ok(shipment.Id);
    }

    public async Task<Result<ShipmentResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = _correlation.CorrelationId,
            ["ShipmentId"] = id
        });

        var shipment = await _db.Shipments
            .AsNoTracking()
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (shipment is null)
        {
            _logShipmentGetNotFound(_logger, id, null);

            return Result<ShipmentResponse>.Fail(
                ErrorCodes.NotFound,
                $"Shipment '{id}' not found.",
                new Dictionary<string, object?> { ["shipmentId"] = id });
        }

        return Result<ShipmentResponse>.Ok(Map(shipment));
    }

    public async Task<Result<IReadOnlyList<ShipmentResponse>>> GetListAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var shipments = await _db.Shipments
            .AsNoTracking()
            .Include(x => x.Documents)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = shipments.Select(Map).ToList();
        return Result<IReadOnlyList<ShipmentResponse>>.Ok(result);
    }

    public async Task<Result<UploadDocumentResult>> UploadDocumentAsync(
        Guid shipmentId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = _correlation.CorrelationId,
            ["ShipmentId"] = shipmentId
        });

        if (fileStream is null)
        {
            _logDocumentUploadValidationFailed(_logger, ErrorCodes.Validation, new { field = "fileStream", reason = "required" }, null);
            return Result<UploadDocumentResult>.Fail(ErrorCodes.Validation, "File stream is required.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logDocumentUploadValidationFailed(_logger, ErrorCodes.Validation, new { field = "fileName", reason = "required" }, null);
            return Result<UploadDocumentResult>.Fail(ErrorCodes.Validation, "File name is required.");
        }

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();

        long size;
        try { size = fileStream.Length; }
        catch { size = -1; }

        _logDocumentUploadStarted(_logger, shipmentId, fileName, size, null);

        var shipment = await _db.Shipments
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == shipmentId, ct);

        if (shipment is null)
        {
            _logDocumentUploadShipmentNotFound(_logger, shipmentId, null);

            return Result<UploadDocumentResult>.Fail(
                ErrorCodes.NotFound,
                $"Shipment '{shipmentId}' not found.",
                new Dictionary<string, object?> { ["shipmentId"] = shipmentId });
        }

        var safeFileName = fileName.Replace("/", "_").Replace("\\", "_").Trim();
        var blobName = $"{shipmentId}/{Guid.NewGuid()}-{safeFileName}";

        //1. Upload to Blob
        try
        {
            await _blob.UploadAsync(fileStream, blobName, contentType, ct);
        }
        catch (Exception ex)
        {
            _logBlobUploadFailed(_logger, shipmentId, blobName, contentType, ex);

            return Result<UploadDocumentResult>.Fail(
                ErrorCodes.ExternalDependency,
                "Blob upload failed.",
                new Dictionary<string, object?>
                {
                    ["blobName"] = blobName,
                    ["exceptionType"] = ex.GetType().Name
                });
        }

        _logBlobUploaded(_logger, shipmentId, blobName, null);

        //2. Save document + update status
        var document = new ShipmentDocument(
            shipmentId: shipmentId,
            blobName: blobName,
            fileName: safeFileName,
            contentType: contentType,
            size: size < 0 ? 0 : size);

        _db.ShipmentDocuments.Add(document);
        shipment.MarkDocumentUploaded();

        await _db.SaveChangesAsync(ct);

        _logShipmentStatusUpdated(_logger, shipmentId, null);

        //3. Outbox - ensures publish can be retried by background dispatcher
        try
        {
            var message = new DocumentUploadedMessage
            {
                ShipmentId = shipmentId,
                BlobName = blobName,
                CorrelationId = _correlation.CorrelationId
            };

            var payload = JsonSerializer.Serialize(message);

            await _outboxWriter.EnqueueAsync(
                type: "DocumentUploaded",
                payload: payload,
                correlationId: message.CorrelationId,
                ct: ct);

            await _db.SaveChangesAsync(ct);

            _logOutboxEnqueued(_logger, shipmentId, blobName, null);
        }
        catch (Exception ex)
        {
            _logOutboxEnqueueFailed(_logger, shipmentId, blobName, ex);

            // Document is already stored; we return an error so API can surface the issue.
            return Result<UploadDocumentResult>.Fail(
                ErrorCodes.ExternalDependency,
                "Service Bus publish failed after successful upload. Document is stored, but processing is not scheduled.",
                new Dictionary<string, object?>
                {
                    ["blobName"] = blobName,
                    ["documentId"] = document.Id,
                    ["exceptionType"] = ex.GetType().Name
                });
        }

        return Result<UploadDocumentResult>.Ok(new UploadDocumentResult
        {
            DocumentId = document.Id,
            BlobName = blobName
        });
    }

    private static ShipmentResponse Map(Shipment s)
        => new()
        {
            Id = s.Id,
            ReferenceNumber = s.ReferenceNumber,
            Sender = s.Sender,
            Recipient = s.Recipient,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            ProcessedAt = s.ProcessedAt,
            Documents = [.. s.Documents.Select(d => new ShipmentDocumentResponse
            {
                Id = d.Id,
                FileName = d.FileName,
                BlobName = d.BlobName,
                Size = d.Size,
                UploadedAt = d.UploadedAt
            })]
        };
}
