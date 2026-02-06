using Microsoft.EntityFrameworkCore;
using Shipments.Domain;
using Shipments.Infrastructure.Outbox;

namespace Shipments.Application.Abstraction;

public interface IShipmentsDbContext
{
    public DbSet<Shipment> Shipments { get; }
    public DbSet<ShipmentDocument> ShipmentDocuments { get; }
    public DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}