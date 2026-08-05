using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests;

public static class TestDbContextFactory
{
    public static FinancialEngineDbContext Create() =>
        new(BuildOptions(Guid.NewGuid().ToString()));

    public static FinancialEngineDbContext CreateForCompany(string databaseName, Guid? companyId)
    {
        var companyContext = new CompanyContext();
        companyContext.SetCompany(companyId);
        return new FinancialEngineDbContext(BuildOptions(databaseName), companyContext);
    }

    private static DbContextOptions<FinancialEngineDbContext> BuildOptions(string databaseName) =>
        new DbContextOptionsBuilder<FinancialEngineDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
}
