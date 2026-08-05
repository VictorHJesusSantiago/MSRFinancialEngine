using System.Text;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Import;

public class ImportServiceTests
{
    [Fact]
    public async Task Imports_new_transactions_and_skips_duplicates_on_reimport()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Empresa Teste", BaseCurrencyCode = "BRL" };
        var source = new Source { CompanyId = company.Id, Name = "Extrato CSV", Type = SourceType.BankStatementCsv, ConfigJson = "{}" };
        context.Companies.Add(company);
        context.Sources.Add(source);
        await context.SaveChangesAsync();

        var sourceRepo = new EfRepository<Source>(context);
        var rawRepo = new EfRepository<RawTransaction>(context);
        var canonicalRepo = new EfRepository<CanonicalTransaction>(context);
        var auditRepo = new EfRepository<AuditEvent>(context);
        var unitOfWork = new EfUnitOfWork(context);

        var factory = new SourceImporterFactory(new ISourceImporter[] { new CsvBankStatementImporter() });
        var auditService = new AuditService(auditRepo);
        var importService = new ImportService(factory, sourceRepo, rawRepo, canonicalRepo, auditService, TestMetrics.Create(), unitOfWork);

        var csv = "Date,Amount,Currency,Description,Reference,Account\n" +
                  "2026-01-10,100.50,BRL,Pagamento Fornecedor,NF-001,CC-123\n";

        var firstImport = await importService.ImportAsync(source.Id,
            new MemoryStream(Encoding.UTF8.GetBytes(csv)));
        Assert.Equal(1, firstImport.Imported);
        Assert.Equal(0, firstImport.Duplicates);

        var secondImport = await importService.ImportAsync(source.Id,
            new MemoryStream(Encoding.UTF8.GetBytes(csv)));
        Assert.Equal(0, secondImport.Imported);
        Assert.Equal(1, secondImport.Duplicates);

        Assert.Single(context.CanonicalTransactions);
    }
}
