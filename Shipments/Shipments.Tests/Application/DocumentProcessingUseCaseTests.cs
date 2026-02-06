using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shipments.Application.Abstraction;
using Shipments.Application.Abstractions;
using Shipments.Application.Messages;
using Shipments.Application.UseCases;
using Shipments.Domain;
using Shipments.Tests.Helpers;

namespace Shipments.Tests.Application;

public sealed class DocumentProcessingUseCaseTests
{
    [Fact]
    public async Task ProcessAsync_WhenShipmentNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();
        var blob = new Mock<IBlobStorage>(MockBehavior.Strict);

        var sut = new DocumentProcessingUseCase(db, blob.Object, NullLogger<DocumentProcessingUseCase>.Instance);

        var msg = new DocumentUploadedMessage
        {
            ShipmentId = Guid.NewGuid(),
            BlobName = "x",
            CorrelationId = "corr-1"
        };

        // Act
        var result = await sut.ProcessAsync(msg, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("not_found");

        blob.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenAlreadyProcessed_ReturnsOk_AndDoesNotDownloadBlob()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();

        var shipment = Shipment.Create("REF-1", "Sender", "Recipient");
        shipment.MarkProcessed(); // already processed
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var blob = new Mock<IBlobStorage>(MockBehavior.Strict);

        var sut = new DocumentProcessingUseCase(db, blob.Object, NullLogger<DocumentProcessingUseCase>.Instance);

        var msg = new DocumentUploadedMessage
        {
            ShipmentId = shipment.Id,
            BlobName = "shipments/a.pdf",
            CorrelationId = "corr-2"
        };

        // Act
        var result = await sut.ProcessAsync(msg, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // no blob calls
        blob.VerifyNoOtherCalls();

        // shipment remains processed
        var reloaded = await db.Shipments.FindAsync(shipment.Id);
        reloaded!.Status.Should().Be(ShipmentStatus.Processed);
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_DownloadsBlob_AndMarksShipmentProcessed()
    {
        // Arrange
        await using var db = TestDbContextFactory.Create();

        var shipment = Shipment.Create("REF-2", "Sender", "Recipient");
        // Ensure not processed initially
        shipment.Status.Should().NotBe(ShipmentStatus.Processed);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var blob = new Mock<IBlobStorage>(MockBehavior.Strict);

        // Return a readable stream
        blob.Setup(x => x.DownloadAsync("blob-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        var sut = new DocumentProcessingUseCase(db, blob.Object, NullLogger<DocumentProcessingUseCase>.Instance);

        var msg = new DocumentUploadedMessage
        {
            ShipmentId = shipment.Id,
            BlobName = "blob-1",
            CorrelationId = "corr-3"
        };

        // Act
        var result = await sut.ProcessAsync(msg, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        blob.Verify(x => x.DownloadAsync("blob-1", It.IsAny<CancellationToken>()), Times.Once);

        var reloaded = await db.Shipments.FindAsync(shipment.Id);
        reloaded!.Status.Should().Be(ShipmentStatus.Processed);
    }
}
