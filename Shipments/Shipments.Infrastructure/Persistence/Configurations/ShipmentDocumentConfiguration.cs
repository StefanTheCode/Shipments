using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipments.Domain;

namespace Shipments.Infrastructure.Persistence.Configurations;

public class ShipmentDocumentConfiguration : IEntityTypeConfiguration<ShipmentDocument>
{
    public void Configure(EntityTypeBuilder<ShipmentDocument> b)
    {
        b.ToTable("shipment_documents");

        b.HasKey(x => x.Id);

        b.Property(x => x.ShipmentId)
            .HasColumnName("shipment_id")
            .IsRequired();

        b.Property(x => x.BlobName)
            .HasColumnName("blob_name")
            .HasMaxLength(512)
            .IsRequired();

        b.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(128)
            .IsRequired();

        b.Property(x => x.Size)
            .HasColumnName("size")
            .IsRequired();

        b.Property(x => x.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        b.Property(x => x.ContentSha256)
            .HasColumnName("content_sha256")
            .HasMaxLength(64);

        // "Senior": indeks za brži lookup po shipmentu
        b.HasIndex(x => x.ShipmentId);

        // "Senior": sprečava duple unose istog blob-a za istu pošiljku
        b.HasIndex(x => new { x.ShipmentId, x.BlobName }).IsUnique();
    }
}