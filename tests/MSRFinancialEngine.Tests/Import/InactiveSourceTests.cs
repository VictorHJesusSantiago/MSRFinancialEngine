using System.Text;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Import;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Import;

public class InactiveSourceTests
{
    private const string Csv =
        "Date,Amount,Currency,Description,Reference,Account\n2026-01-10,100.50,BRL,Pagamento,NF-1,CC-1\n";

    private static Stream Content() => new MemoryStream(Encoding.UTF8.GetBytes(Csv));

    private static ImportService BuildImportService(FinancialEngineDbContext context) => new(
        new SourceImporterFactory(new ISourceImporter[] { new CsvBankStatementImporter() }),
        new EfRepository<Source>(context),
        new EfRepository<RawTransaction>(context),
        new EfRepository<CanonicalTransaction>(context),
        new AuditService(new EfRepository<AuditEvent>(context)),
        TestMetrics.Create(),
        new EfUnitOfWork(context));

    private static async Task<Source> SeedSourceAsync(FinancialEngineDbContext context, bool active)
    {
        var company = new Company { Name = "Empresa", BaseCurrencyCode = "BRL" };
        var source = new Source
        {
            CompanyId = company.Id, Name = "Extrato antigo",
            Type = SourceType.BankStatementCsv, ConfigJson = "{}", Active = active
        };
        context.Companies.Add(company);
        context.Sources.Add(source);
        await context.SaveChangesAsync();
        return source;
    }

    [Fact]
    public async Task Importing_from_a_deactivated_source_is_refused()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context, active: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildImportService(context).ImportAsync(source.Id, Content()));

        Assert.Contains("desativada", ex.Message);
        Assert.Empty(context.CanonicalTransactions);
    }

    [Fact]
    public async Task Importing_from_an_active_source_still_works()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context, active: true);

        var result = await BuildImportService(context).ImportAsync(source.Id, Content());

        Assert.Equal(1, result.Imported);
    }

    [Fact]
    public async Task Queueing_for_a_deactivated_source_is_refused_upfront()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context, active: false);

        var jobService = new ImportJobService(
            new EfRepository<ImportJob>(context),
            new EfRepository<Source>(context),
            BuildImportService(context),
            new ImportJobSignal(),
            new AuditService(new EfRepository<AuditEvent>(context)),
            new EfUnitOfWork(context),
            new InMemoryStagingStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            jobService.EnqueueAsync(source.Id, "extrato.csv", Content()));

        Assert.Empty(context.ImportJobs);
    }
}
