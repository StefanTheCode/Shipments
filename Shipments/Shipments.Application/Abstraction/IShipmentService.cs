using Shipments.Application.Dtos;
using Shipments.Application.Results;

namespace Shipments.Application.Abstraction;

public interface IShipmentService
{
    Task<Result<Guid>> CreateAsync(CreateShipmentRequest request, CancellationToken ct);
    Task<Result<ShipmentResponse>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<IReadOnlyList<ShipmentResponse>>> GetListAsync(int page, int pageSize, CancellationToken ct);

    Task<Result<UploadDocumentResult>> UploadDocumentAsync(
        Guid shipmentId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct);
}