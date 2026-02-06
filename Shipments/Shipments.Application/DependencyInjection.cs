using Microsoft.Extensions.DependencyInjection;
using Shipments.Application.Abstraction;
using Shipments.Application.Abstractions;
using Shipments.Application.Services;
using Shipments.Application.UseCases;

namespace Shipments.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentProcessingUseCase, DocumentProcessingUseCase>();
        services.AddScoped<IShipmentService, ShipmentService>();

        return services;
    }
}
