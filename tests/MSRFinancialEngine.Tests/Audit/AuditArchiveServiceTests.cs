using System.Text;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Audit;

internal class InMemoryArchiveStore : IAuditArchiveStore
{
    private readonly Dictionary<string, byte[]> _files = new();

    public Task<string> WriteAsync(string name, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _files[name] = buffer.ToArray();
        return Task.FromResult(name);
    }

    public Task<Stream?> OpenReadAsync(string location, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(_files.TryGetValue(location, out var bytes) ? new MemoryStream(bytes) : null);

    public string ReadText(string location) => Encoding.UTF8.GetString(_files[location]);

    public void Corrupt(string location) => _files[location] = Encoding.UTF8.GetBytes("adulterado");
}

public class AuditArchiveServiceTests
{
    private static (AuditArchiveService service, InMemoryArchiveStore store) Build(FinancialEngineDbContext context)
    {
        var store = new InMemoryArchiveStore();
        var service = new AuditArchiveService(
            new EfRepository<AuditEvent>(context),
            new EfRepository<AuditArchive>(context),
            store,
            new AuditService(new EfRepository<AuditEvent>(context)),
            new EfUnitOfWork(context));

        return (service, store);
    }

    private static void SeedEvents(FinancialEngineDbContext context, params DateTime[] timestamps)
    {
        foreach (var ts in timestamps)
            context.AuditEvents.Add(new AuditEvent
            {
                EntityType = "Teste", Action = "Acao", Timestamp = ts, DetailsJson = "{}"
            });

        context.SaveChanges();
    }

    [Fact]
    public async Task Archiving_writes_one_line_per_event_and_records_the_period()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10), now.AddDays(-9), now.AddDays(-8));

        var (service, store) = Build(context);
        var archive = await service.ArchiveAsync(now.AddDays(-11), now.AddDays(-7), null);

        Assert.Equal(3, archive.EventCount);
        Assert.False(string.IsNullOrWhiteSpace(archive.ContentHash));

        var lines = store.ReadText(archive.Location)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("{", l.Trim()));
    }

    [Fact]
    public async Task Only_events_inside_the_period_are_archived()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-30), now.AddDays(-10), now.AddDays(-2));

        var (service, _) = Build(context);
        var archive = await service.ArchiveAsync(now.AddDays(-15), now.AddDays(-5), null);

        Assert.Equal(1, archive.EventCount);
    }

    [Fact]
    public async Task Verification_confirms_an_untouched_copy()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10));

        var (service, _) = Build(context);
        var archive = await service.ArchiveAsync(now.AddDays(-11), now.AddDays(-9), null);

        Assert.True(await service.VerifyAsync(archive.Id));
    }

    [Fact]
    public async Task Verification_detects_a_tampered_copy()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10));

        var (service, store) = Build(context);
        var archive = await service.ArchiveAsync(now.AddDays(-11), now.AddDays(-9), null);

        store.Corrupt(archive.Location);

        Assert.False(await service.VerifyAsync(archive.Id));
    }

    [Fact]
    public async Task Verification_fails_when_the_copy_disappeared()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10));

        var (service, _) = Build(context);
        var archive = await service.ArchiveAsync(now.AddDays(-11), now.AddDays(-9), null);

        var stored = context.AuditArchives.Single(a => a.Id == archive.Id);
        stored.Location = "sumiu.jsonl";
        await context.SaveChangesAsync();

        Assert.False(await service.VerifyAsync(archive.Id));
    }

    [Fact]
    public async Task Archiving_a_period_that_reaches_the_present_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        SeedEvents(context, DateTime.UtcNow.AddMinutes(-5));

        var (service, _) = Build(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ArchiveAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, null));
    }

    [Fact]
    public async Task Archiving_an_empty_period_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        var (service, _) = Build(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ArchiveAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9), null));
    }

    [Fact]
    public async Task An_inverted_period_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        var (service, _) = Build(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ArchiveAsync(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-10), null));
    }
}
