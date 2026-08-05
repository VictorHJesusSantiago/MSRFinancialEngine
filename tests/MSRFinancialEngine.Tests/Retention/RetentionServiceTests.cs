using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Retention;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Retention;

public class RetentionServiceTests
{
    private static RetentionService Build(FinancialEngineDbContext context, RetentionOptions options) => new(
        new EfRepository<RefreshToken>(context),
        new EfRepository<ImportJob>(context),
        new EfRepository<AuditEvent>(context),
        new EfRepository<AuditArchive>(context),
        new AuditService(new EfRepository<AuditEvent>(context)),
        new EfUnitOfWork(context),
        options);

    private static ApplicationUser AddUser(FinancialEngineDbContext context)
    {
        var user = new ApplicationUser { Name = "U", Email = $"{Guid.NewGuid():N}@x.com", Role = UserRole.Analyst };
        context.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task Old_revoked_and_expired_tokens_are_purged_but_active_ones_survive()
    {
        await using var context = TestDbContextFactory.Create();
        var user = AddUser(context);
        var now = DateTime.UtcNow;

        var revokedLongAgo = new RefreshToken
        {
            UserId = user.Id, TokenHash = "a",
            ExpiresAtUtc = now.AddDays(5), RevokedAtUtc = now.AddDays(-60)
        };
        var expiredLongAgo = new RefreshToken
        {
            UserId = user.Id, TokenHash = "b", ExpiresAtUtc = now.AddDays(-60)
        };
        var revokedRecently = new RefreshToken
        {
            UserId = user.Id, TokenHash = "c",
            ExpiresAtUtc = now.AddDays(5), RevokedAtUtc = now.AddDays(-1)
        };
        var active = new RefreshToken
        {
            UserId = user.Id, TokenHash = "d", ExpiresAtUtc = now.AddDays(5)
        };

        context.RefreshTokens.AddRange(revokedLongAgo, expiredLongAgo, revokedRecently, active);
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions { RefreshTokenDays = 30 }).PurgeAsync();

        Assert.Equal(2, result.RefreshTokensRemoved);

        var remaining = context.RefreshTokens.Select(t => t.TokenHash).OrderBy(h => h).ToList();
        Assert.Equal(new[] { "c", "d" }, remaining);
    }

    [Fact]
    public async Task Finished_import_jobs_are_purged_but_pending_ones_are_kept()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "E", BaseCurrencyCode = "BRL" };
        var source = new Source { CompanyId = company.Id, Name = "S", Type = SourceType.BankStatementCsv };
        context.Companies.Add(company);
        context.Sources.Add(source);

        var now = DateTime.UtcNow;
        var completedOld = new ImportJob
        {
            SourceId = source.Id, FileName = "velho.csv", Status = ImportJobStatus.Completed,
            FinishedAtUtc = now.AddDays(-200)
        };
        var failedOld = new ImportJob
        {
            SourceId = source.Id, FileName = "falhou.csv", Status = ImportJobStatus.Failed,
            FinishedAtUtc = now.AddDays(-200)
        };
        var completedRecent = new ImportJob
        {
            SourceId = source.Id, FileName = "recente.csv", Status = ImportJobStatus.Completed,
            FinishedAtUtc = now.AddDays(-1)
        };
        var stuckQueued = new ImportJob
        {
            SourceId = source.Id, FileName = "preso.csv", Status = ImportJobStatus.Queued,
            CreatedAtUtc = now.AddDays(-300)
        };

        context.ImportJobs.AddRange(completedOld, failedOld, completedRecent, stuckQueued);
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions { ImportJobDays = 90 }).PurgeAsync();

        Assert.Equal(2, result.ImportJobsRemoved);

        var remaining = context.ImportJobs.Select(j => j.FileName).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "preso.csv", "recente.csv" }, remaining);
    }

    [Fact]
    public async Task Audit_events_are_kept_by_default()
    {
        await using var context = TestDbContextFactory.Create();

        context.AuditEvents.Add(new AuditEvent
        {
            EntityType = "X", Action = "Y", Timestamp = DateTime.UtcNow.AddYears(-5)
        });
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions()).PurgeAsync();

        Assert.Equal(0, result.AuditEventsRemoved);
        Assert.Single(context.AuditEvents);
    }

    [Fact]
    public async Task Old_audit_events_are_not_purged_while_no_archive_covers_them()
    {
        await using var context = TestDbContextFactory.Create();

        context.AuditEvents.Add(new AuditEvent
        {
            EntityType = "X", Action = "Antigo", Timestamp = DateTime.UtcNow.AddDays(-400)
        });
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions { AuditEventDays = 365 }).PurgeAsync();

        Assert.Equal(0, result.AuditEventsRemoved);
        Assert.Single(context.AuditEvents);
    }

    [Fact]
    public async Task Only_events_actually_archived_are_purged()
    {
        await using var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;

        var arquivado = new AuditEvent
        {
            EntityType = "X", Action = "Arquivado",
            Timestamp = now.AddDays(-400), ArchivedAtUtc = now.AddDays(-390)
        };
        var foraDoArquivo = new AuditEvent
        {
            EntityType = "X", Action = "ForaDoArquivo", Timestamp = now.AddDays(-400)
        };
        var recente = new AuditEvent { EntityType = "X", Action = "Recente", Timestamp = now.AddDays(-1) };
        context.AuditEvents.AddRange(arquivado, foraDoArquivo, recente);
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions { AuditEventDays = 365 }).PurgeAsync();

        Assert.Equal(1, result.AuditEventsRemoved);

        var restantes = context.AuditEvents.Select(e => e.Action).ToList();
        Assert.Contains("ForaDoArquivo", restantes);
        Assert.Contains("Recente", restantes);
        Assert.DoesNotContain("Arquivado", restantes);
    }

    [Fact]
    public async Task The_purge_itself_is_recorded_in_the_audit_trail()
    {
        await using var context = TestDbContextFactory.Create();
        var user = AddUser(context);

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = "a", ExpiresAtUtc = DateTime.UtcNow.AddDays(-60)
        });
        await context.SaveChangesAsync();

        await Build(context, new RetentionOptions { RefreshTokenDays = 30 }).PurgeAsync();

        Assert.Contains(context.AuditEvents, e => e.Action == "Purged");
    }

    [Fact]
    public async Task Nothing_is_removed_when_retention_is_disabled()
    {
        await using var context = TestDbContextFactory.Create();
        var user = AddUser(context);

        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = "a", ExpiresAtUtc = DateTime.UtcNow.AddDays(-999)
        });
        await context.SaveChangesAsync();

        var result = await Build(context, new RetentionOptions
        {
            RefreshTokenDays = 0, ImportJobDays = 0, AuditEventDays = 0
        }).PurgeAsync();

        Assert.Equal(0, result.RefreshTokensRemoved);
        Assert.Single(context.RefreshTokens);
    }
}
