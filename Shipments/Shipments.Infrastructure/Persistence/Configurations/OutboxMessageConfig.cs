using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipments.Infrastructure.Outbox;

namespace Shipments.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfig : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Payload).IsRequired();

        b.HasIndex(x => x.DispatchedAt);
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => x.LockedUntil);
    }
}