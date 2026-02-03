using Microsoft.EntityFrameworkCore;
using Shipments.Domain;

namespace Shipments.Infrastructure.Persistence;

public class ShipmentsDbContext : DbContext
{
    public ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options) : base(options) { }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentDocument> ShipmentDocuments => Set<ShipmentDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentsDbContext).Assembly);
    }
}