using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipments.Domain;

namespace Shipments.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("shipments");

        b.HasKey(x => x.Id);

        b.Property(x => x.ReferenceNumber)
            .HasColumnName("reference_number")
            .HasMaxLength(64)
            .IsRequired();

        b.HasIndex(x => x.ReferenceNumber)
            .IsUnique();

        b.Property(x => x.Sender)
            .HasColumnName("sender")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Recipient)
            .HasColumnName("recipient")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        b.Property(x => x.ProcessedAt)
            .HasColumnName("processed_at");

        b.HasMany(x => x.Documents)
            .WithOne(x => x.Shipment)
            .HasForeignKey(x => x.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}