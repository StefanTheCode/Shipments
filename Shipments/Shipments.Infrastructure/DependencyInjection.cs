using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shipments.Application.Abstraction;
using Shipments.Application.Abstractions;
using Shipments.Infrastructure.Configuration;
using Shipments.Infrastructure.Messaging;
using Shipments.Infrastructure.Outbox;
using Shipments.Infrastructure.Persistence;
using Shipments.Infrastructure.Processing;
using Shipments.Infrastructure.Storage;

namespace Shipments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config,
    bool hostBackgroundServices)
    {
        services.AddDbContext<ShipmentsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("ShipmentsDb")));

        services.AddScoped<IShipmentsDbContext>(sp => sp.GetRequiredService<ShipmentsDbContext>());

        services.Configure<RuntimeOptions>(config.GetSection(RuntimeOptions.SectionName));
        services.Configure<BlobStorageOptions>(config.GetSection(BlobStorageOptions.SectionName));
        services.Configure<ServiceBusOptions>(config.GetSection(ServiceBusOptions.SectionName));

        services.AddScoped<IOutboxWriter, EfOutboxWriter>();

        services.AddHostedService<OutboxDispatcherHostedService>();

        var runtime = config
           .GetSection(RuntimeOptions.SectionName)
           .Get<RuntimeOptions>() ?? new();

        if (runtime.UseAzure)
        {
            services.AddSingleton<IBlobStorage, AzureBlobStorage>();
        }
        else
        {
            services.AddSingleton<IBlobStorage, LocalFileBlobStorage>();
        }

        if (runtime.UseAzure)
        {
            services.AddSingleton<IMessagePublisher, AzureServiceBusPublisher>();

            if (hostBackgroundServices)
            {
                services.AddHostedService<ServiceBusDocumentProcessorHostedService>();
            }
        }
        else
        {
            services.AddSingleton<IDocumentProcessingQueue, InMemoryDocumentProcessingQueue>();
            services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();

            if (hostBackgroundServices)
            {
                services.AddHostedService<DocumentProcessingBackgroundService>();
            }
        }

        return services;
    }
}