using Microsoft.EntityFrameworkCore;
using Shipments.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShipmentsDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("ShipmentsDb");
    opt.UseNpgsql(cs);
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();