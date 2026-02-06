using Microsoft.EntityFrameworkCore;
using Shipments.Application.Abstraction;
using Shipments.Domain;
using Shipments.Infrastructure.Outbox;

namespace Shipments.Infrastructure.Persistence;

public sealed class ShipmentsDbContext
    : DbContext, IShipmentsDbContext
{
    public ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentsDbContext).Assembly);
    }
}