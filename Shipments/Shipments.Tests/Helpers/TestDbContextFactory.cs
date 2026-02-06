using Microsoft.EntityFrameworkCore;
using Shipments.Infrastructure.Persistence;

namespace Shipments.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ShipmentsDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString("N");

        var opts = new DbContextOptionsBuilder<ShipmentsDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;

        return new ShipmentsDbContext(opts);
    }
}