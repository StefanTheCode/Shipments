using Carter;
using Serilog;
using Shipments.Api.Observability;
using Shipments.Application.Abstractions;
using Shipments.Application.DependencyInjection;
using Shipments.Infrastructure;
using Shipments.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .ReadFrom.Services(services);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCarter();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    hostBackgroundServices: !builder.Configuration
        .GetSection("Runtime")
        .Get<RuntimeOptions>()!
        .UseAzure);

builder.Services.AddOpenApi();

builder.Services.Configure<CorrelationOptions>(
    builder.Configuration.GetSection(CorrelationOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdMiddleware>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

app.MapCarter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();