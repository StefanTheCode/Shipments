using Carter;
using Microsoft.AspNetCore.Mvc;
using Shipments.Api.Extensions;
using Shipments.Application.Abstraction;
using Shipments.Application.Dtos;

namespace Shipments.Api.Modules;

public sealed class ShipmentsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shipments")
            .WithTags("Shipments");

        // POST /api/shipments
        group.MapPost("", async (
            [FromBody] CreateShipmentRequest request,
            IShipmentService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            var id = result.Value!;
            var location = $"/api/shipments/{id}";

            return Results.Created(location, new { id });
        })
        .WithName("CreateShipment")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /api/shipments/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            IShipmentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetShipmentById")
        .Produces<ShipmentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/shipments?page=1&pageSize=20
        group.MapGet("", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IShipmentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetListAsync(page, pageSize, ct);
            return result.ToHttpResult();
        })
        .WithName("GetShipments")
        .Produces<IReadOnlyList<ShipmentResponse>>(StatusCodes.Status200OK);

        // POST /api/shipments/{id}/documents (multipart/form-data)
        group.MapPost("/{id:guid}/documents", async (
            Guid id,
            IFormFile file,
            IShipmentService service,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("File is required. Use form field name 'file'.");
            }

            await using var stream = file.OpenReadStream();

            var result = await service.UploadDocumentAsync(
                shipmentId: id,
                fileStream: stream,
                fileName: file.FileName,
                contentType: file.ContentType,
                ct: ct);

            return result.ToHttpResult();
        })
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery()
        .WithName("UploadShipmentDocument");

    }
}