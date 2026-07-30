using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests;

public static class TestDbContextFactory
{
    public static FinancialEngineDbContext Create()
    {
        var options = new DbContextOptionsBuilder<FinancialEngineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FinancialEngineDbContext(options);
    }
}
