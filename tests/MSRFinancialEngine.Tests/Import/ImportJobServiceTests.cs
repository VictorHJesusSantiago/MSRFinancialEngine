using System.Text;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Import;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Import;

internal class InMemoryStagingStore : IImportStagingStore
{
    private readonly Dictionary<string, byte[]> _files = new();

    public int RemainingFiles => _files.Count;

    public Task<string> StageAsync(Guid jobId, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var path = $"mem://{jobId:N}";
        _files[path] = buffer.ToArray();
        return Task.FromResult(path);
    }

    public Stream OpenRead(string path) => new MemoryStream(_files[path]);

    public void Discard(string path) => _files.Remove(path);
}

public class ImportJobServiceTests
{
    private const string Csv =
        "Date,Amount,Currency,Description,Reference,Account\n" +
        "2026-01-10,100.50,BRL,Pagamento,NF-1,CC-1\n" +
        "2026-01-11,250.00,BRL,Recebimento,NF-2,CC-1\n";

    private static (ImportJobService service, PostgresImportJobClaimer claimer, InMemoryStagingStore staging)
        Build(FinancialEngineDbContext context)
    {
        var claimer = new PostgresImportJobClaimer(context);
        var staging = new InMemoryStagingStore();

        var importService = new ImportService(
            new SourceImporterFactory(new ISourceImporter[] { new CsvBankStatementImporter() }),
            new EfRepository<Source>(context),
            new EfRepository<RawTransaction>(context),
            new EfRepository<CanonicalTransaction>(context),
            new AuditService(new EfRepository<AuditEvent>(context)),
            TestMetrics.Create(),
            new EfUnitOfWork(context));

        var service = new ImportJobService(
            new EfRepository<ImportJob>(context),
            new EfRepository<Source>(context),
            importService,
            new ImportJobSignal(),
            new AuditService(new EfRepository<AuditEvent>(context)),
            new EfUnitOfWork(context),
            staging);

        return (service, claimer, staging);
    }

    private static async Task<Source> SeedSourceAsync(FinancialEngineDbContext context)
    {
        var company = new Company { Name = "Empresa", BaseCurrencyCode = "BRL" };
        var source = new Source
        {
            CompanyId = company.Id, Name = "CSV", Type = SourceType.BankStatementCsv, ConfigJson = "{}"
        };
        context.Companies.Add(company);
        context.Sources.Add(source);
        await context.SaveChangesAsync();
        return source;
    }

    private static Stream Content(string csv = Csv) => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task Enqueue_registers_the_job_without_importing_yet()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        var job = await service.EnqueueAsync(source.Id, "extrato.csv", Content());

        Assert.Equal(ImportJobStatus.Queued, job.Status);
        Assert.Empty(context.CanonicalTransactions);

        Assert.Equal(job.Id, await claimer.TryClaimNextAsync());
    }

    [Fact]
    public async Task A_claimed_job_is_not_handed_to_a_second_consumer()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        await service.EnqueueAsync(source.Id, "extrato.csv", Content());

        Assert.NotNull(await claimer.TryClaimNextAsync());
        Assert.Null(await claimer.TryClaimNextAsync());
    }

    [Fact]
    public async Task Jobs_are_claimed_in_arrival_order()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        var first = await service.EnqueueAsync(source.Id, "primeiro.csv", Content());
        await Task.Delay(10);
        var second = await service.EnqueueAsync(source.Id, "segundo.csv", Content());

        Assert.Equal(first.Id, await claimer.TryClaimNextAsync());
        Assert.Equal(second.Id, await claimer.TryClaimNextAsync());
    }

    [Fact]
    public async Task A_job_orphaned_by_a_crashed_instance_returns_to_the_queue()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        var job = await service.EnqueueAsync(source.Id, "extrato.csv", Content());
        await claimer.TryClaimNextAsync();

        var claimed = context.ImportJobs.Single(j => j.Id == job.Id);
        claimed.StartedAtUtc = DateTime.UtcNow.AddHours(-2);
        await context.SaveChangesAsync();

        Assert.Null(await claimer.TryClaimNextAsync());

        var reclaimed = await claimer.ReclaimStaleAsync(TimeSpan.FromMinutes(30));

        Assert.Equal(1, reclaimed);
        Assert.Equal(job.Id, await claimer.TryClaimNextAsync());
    }

    [Fact]
    public async Task A_job_still_progressing_is_not_reclaimed()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        await service.EnqueueAsync(source.Id, "extrato.csv", Content());
        await claimer.TryClaimNextAsync();

        Assert.Equal(0, await claimer.ReclaimStaleAsync(TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public async Task Processing_imports_the_file_and_records_the_totals()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, staging) = Build(context);

        var job = await service.EnqueueAsync(source.Id, "extrato.csv", Content());
        await claimer.TryClaimNextAsync();
        await service.ProcessAsync(job.Id);

        await context.Entry(job).ReloadAsync();

        Assert.Equal(ImportJobStatus.Completed, job.Status);
        Assert.Equal(2, job.TotalParsed);
        Assert.Equal(2, job.Imported);
        Assert.Equal(0, job.Duplicates);
        Assert.NotNull(job.FinishedAtUtc);
        Assert.Equal(2, context.CanonicalTransactions.Count());

        Assert.Equal(0, staging.RemainingFiles);
    }

    [Fact]
    public async Task A_malformed_file_marks_the_job_as_failed_with_the_reason()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, staging) = Build(context);

        var invalid = "Date,Amount,Currency,Description,Reference,Account\nnao-e-data,100.50,BRL,X,,\n";
        var job = await service.EnqueueAsync(source.Id, "quebrado.csv", Content(invalid));

        await claimer.TryClaimNextAsync();
        await service.ProcessAsync(job.Id);
        await context.Entry(job).ReloadAsync();

        Assert.Equal(ImportJobStatus.Failed, job.Status);
        Assert.False(string.IsNullOrWhiteSpace(job.ErrorMessage));
        Assert.Empty(context.CanonicalTransactions);
        Assert.Equal(0, staging.RemainingFiles);
    }

    [Fact]
    public async Task Processing_the_same_job_twice_does_not_import_twice()
    {
        await using var context = TestDbContextFactory.Create();
        var source = await SeedSourceAsync(context);
        var (service, claimer, _) = Build(context);

        var job = await service.EnqueueAsync(source.Id, "extrato.csv", Content());

        await claimer.TryClaimNextAsync();
        await service.ProcessAsync(job.Id);
        await service.ProcessAsync(job.Id);

        Assert.Equal(2, context.CanonicalTransactions.Count());
    }

    [Fact]
    public async Task Enqueueing_for_an_unknown_source_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        var (service, _, _) = Build(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnqueueAsync(Guid.NewGuid(), "extrato.csv", Content()));
    }
}
