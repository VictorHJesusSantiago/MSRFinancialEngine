using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Import;

public class ReprocessServiceTests
{
    private static ReprocessService Build(FinancialEngineDbContext context) => new(
        new EfRepository<Source>(context),
        new EfRepository<CanonicalTransaction>(context),
        new EfRepository<RawTransaction>(context),
        new EfRepository<Divergence>(context),
        new AuditService(new EfRepository<AuditEvent>(context)),
        new EfUnitOfWork(context));

    [Fact]
    public async Task Invalidating_removes_unreconciled_data_and_preserves_reconciled()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Empresa", BaseCurrencyCode = "BRL" };
        var source = new Source { CompanyId = company.Id, Name = "CSV", Type = SourceType.BankStatementCsv };
        context.Companies.Add(company);
        context.Sources.Add(source);

        var rawPending = new RawTransaction { SourceId = source.Id, PayloadJson = "{\"a\":1}", Normalized = true };
        var rawReconciled = new RawTransaction { SourceId = source.Id, PayloadJson = "{\"b\":2}", Normalized = true };
        context.RawTransactions.AddRange(rawPending, rawReconciled);

        var pending = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = source.Id, RawTransactionId = rawPending.Id,
            Amount = 100m, CurrencyCode = "BRL", TransactionDate = new DateTime(2026, 1, 10),
            Description = "PENDENTE", Hash = "h1", Reconciled = false
        };
        var reconciled = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = source.Id, RawTransactionId = rawReconciled.Id,
            Amount = 200m, CurrencyCode = "BRL", TransactionDate = new DateTime(2026, 1, 11),
            Description = "CONCILIADA", Hash = "h2", Reconciled = true
        };
        context.CanonicalTransactions.AddRange(pending, reconciled);
        context.Divergences.Add(new Divergence { TransactionId = pending.Id, Reason = DivergenceReason.NoCandidate });
        await context.SaveChangesAsync();

        var result = await Build(context).InvalidateSourceAsync(source.Id);

        Assert.Equal(1, result.CanonicalRemoved);
        Assert.Equal(1, result.PreservedBecauseReconciled);
        Assert.Equal(1, result.RawMarkedForReimport);

        Assert.Single(context.CanonicalTransactions);
        Assert.Equal("CONCILIADA", context.CanonicalTransactions.Single().Description);
        Assert.Empty(context.Divergences);

        await context.Entry(rawPending).ReloadAsync();
        await context.Entry(rawReconciled).ReloadAsync();
        Assert.False(rawPending.Normalized);
        Assert.True(rawReconciled.Normalized);
        Assert.Equal(2, context.RawTransactions.Count());
    }

    [Fact]
    public async Task Reimporting_after_invalidation_recreates_the_transactions()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Empresa", BaseCurrencyCode = "BRL" };
        var source = new Source { CompanyId = company.Id, Name = "CSV", Type = SourceType.BankStatementCsv, ConfigJson = "{}" };
        context.Companies.Add(company);
        context.Sources.Add(source);
        await context.SaveChangesAsync();

        var importService = new ImportService(
            new SourceImporterFactory(new ISourceImporter[] { new CsvBankStatementImporter() }),
            new EfRepository<Source>(context),
            new EfRepository<RawTransaction>(context),
            new EfRepository<CanonicalTransaction>(context),
            new AuditService(new EfRepository<AuditEvent>(context)),
            TestMetrics.Create(),
            new EfUnitOfWork(context));

        var csv = "Date,Amount,Currency,Description,Reference,Account\n2026-01-10,100.50,BRL,Pagamento,NF-1,CC-1\n";
        Stream Content() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        await importService.ImportAsync(source.Id, Content());
        Assert.Single(context.CanonicalTransactions);

        var blocked = await importService.ImportAsync(source.Id, Content());
        Assert.Equal(0, blocked.Imported);
        Assert.Equal(1, blocked.Duplicates);

        await Build(context).InvalidateSourceAsync(source.Id);
        Assert.Empty(context.CanonicalTransactions);

        var reimported = await importService.ImportAsync(source.Id, Content());
        Assert.Equal(1, reimported.Imported);
        Assert.Single(context.CanonicalTransactions);
    }
}
