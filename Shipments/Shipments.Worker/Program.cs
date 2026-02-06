using Shipments.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration,
    hostBackgroundServices: true);

var host = builder.Build();
host.Run();
