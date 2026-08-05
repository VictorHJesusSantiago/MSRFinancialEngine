using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Audit;

public class ScheduledArchivingTests
{
    private static AuditArchiveService Build(FinancialEngineDbContext context, IAuditArchiveStore store) => new(
        new EfRepository<AuditEvent>(context),
        new EfRepository<AuditArchive>(context),
        store,
        new AuditService(new EfRepository<AuditEvent>(context)),
        new EfUnitOfWork(context));

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
    public async Task First_run_archives_from_the_oldest_event()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10), now.AddDays(-5), now.AddDays(-3));

        var archive = await Build(context, new InMemoryArchiveStore()).ArchivePendingAsync(lagDays: 1);

        Assert.NotNull(archive);
        Assert.Equal(3, archive!.EventCount);
    }

    [Fact]
    public async Task Recent_events_inside_the_lag_window_are_left_for_the_next_run()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10), now.AddHours(-2));

        var archive = await Build(context, new InMemoryArchiveStore()).ArchivePendingAsync(lagDays: 1);

        Assert.NotNull(archive);
        Assert.Equal(1, archive!.EventCount);
    }

    [Fact]
    public async Task A_second_run_picks_up_only_what_is_still_unarchived()
    {
        await using var context = TestDbContextFactory.Create();
        var store = new InMemoryArchiveStore();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10), now.AddDays(-9));

        var first = await Build(context, store).ArchivePendingAsync(lagDays: 1);
        Assert.Equal(2, first!.EventCount);

        SeedEvents(context, now.AddDays(-5));

        var second = await Build(context, store).ArchivePendingAsync(lagDays: 1);

        Assert.NotNull(second);
        Assert.Equal(1, second!.EventCount);
    }

    [Fact]
    public async Task An_event_arriving_late_inside_an_archived_window_is_still_archived()
    {
        await using var context = TestDbContextFactory.Create();
        var store = new InMemoryArchiveStore();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-10));

        var first = await Build(context, store).ArchivePendingAsync(lagDays: 1);
        Assert.Equal(1, first!.EventCount);

        SeedEvents(context, now.AddDays(-9));

        var second = await Build(context, store).ArchivePendingAsync(lagDays: 1);

        Assert.NotNull(second);
        Assert.Equal(1, second!.EventCount);
    }

    [Fact]
    public async Task Nothing_new_produces_no_archive()
    {
        await using var context = TestDbContextFactory.Create();
        var store = new InMemoryArchiveStore();
        SeedEvents(context, DateTime.UtcNow.AddDays(-10));

        await Build(context, store).ArchivePendingAsync(lagDays: 1);

        var second = await Build(context, store).ArchivePendingAsync(lagDays: 1);

        Assert.Null(second);
    }

    [Fact]
    public async Task An_empty_base_produces_no_archive()
    {
        await using var context = TestDbContextFactory.Create();

        Assert.Null(await Build(context, new InMemoryArchiveStore()).ArchivePendingAsync(lagDays: 1));
    }

    [Fact]
    public async Task Archived_events_become_purgeable_end_to_end()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        SeedEvents(context, now.AddDays(-400));

        var retention = new Application.Retention.RetentionService(
            new EfRepository<RefreshToken>(context),
            new EfRepository<ImportJob>(context),
            new EfRepository<AuditEvent>(context),
            new EfRepository<AuditArchive>(context),
            new AuditService(new EfRepository<AuditEvent>(context)),
            new EfUnitOfWork(context),
            new Application.Retention.RetentionOptions { AuditEventDays = 365 });

        Assert.Equal(0, (await retention.PurgeAsync()).AuditEventsRemoved);

        await Build(context, new InMemoryArchiveStore()).ArchivePendingAsync(lagDays: 1);

        Assert.True((await retention.PurgeAsync()).AuditEventsRemoved >= 1);
    }
}
