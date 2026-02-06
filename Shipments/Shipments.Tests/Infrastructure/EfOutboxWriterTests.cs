using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Shipments.Infrastructure.Outbox;
using Shipments.Tests.Helpers;

public sealed class EfOutboxWriterTests
{
    [Fact]
    public async Task EnqueueAsync_AddsOutboxMessage_AndPersistsAfterSaveChanges()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new EfOutboxWriter(db, NullLogger<EfOutboxWriter>.Instance);

        await sut.EnqueueAsync(
            type: "DocumentUploaded",
            payload: """{"shipmentId":"00000000-0000-0000-0000-000000000001"}""",
            correlationId: "corr-10",
            ct: CancellationToken.None);

        db.OutboxMessages.Local.Should().HaveCount(1);

        await db.SaveChangesAsync();

        db.OutboxMessages.Should().HaveCount(1);
        var msg = db.OutboxMessages.Single();

        msg.Type.Should().Be("DocumentUploaded");
        msg.CorrelationId.Should().Be("corr-10");
        msg.DispatchedAt.Should().BeNull();
        msg.AttemptCount.Should().Be(0);
    }
}
