using MSRFinancialEngine.Application.Reports;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Reports;

public class ReportServiceTests
{
    private static ReportService Build(FinancialEngineDbContext context) => new(
        new EfRepository<CanonicalTransaction>(context),
        new EfRepository<MatchCandidate>(context),
        new EfRepository<Divergence>(context),
        new EfRepository<ApprovalDecision>(context),
        new EfRepository<AuditEvent>(context));

    private static CanonicalTransaction Tx(Guid companyId, string hash, bool reconciled) => new()
    {
        CompanyId = companyId,
        SourceId = Guid.NewGuid(),
        Amount = 100m,
        CurrencyCode = "BRL",
        TransactionDate = new DateTime(2026, 1, 10),
        Description = "TESTE",
        Hash = hash,
        Reconciled = reconciled
    };

    [Fact]
    public async Task Reconciliation_rate_counts_reconciled_and_pending()
    {
        await using var context = TestDbContextFactory.Create();
        var companyId = Guid.NewGuid();

        var a = Tx(companyId, "h1", reconciled: true);
        var b = Tx(companyId, "h2", reconciled: true);
        var c = Tx(companyId, "h3", reconciled: false);
        context.CanonicalTransactions.AddRange(a, b, c);
        context.MatchCandidates.Add(new MatchCandidate
        {
            TransactionAId = a.Id,
            TransactionBId = b.Id,
            Score = 1.0,
            Status = MatchCandidateStatus.AutoApproved
        });
        await context.SaveChangesAsync();

        var report = await Build(context).GetReconciliationRateAsync(companyId);

        Assert.Equal(3, report.TotalTransactions);
        Assert.Equal(2, report.Reconciled);
        Assert.Equal(1, report.Pending);
        Assert.Equal(66.67, report.OverallReconciliationRatePercent);
    }

    [Fact]
    public async Task Reconciliation_rate_of_company_ignores_other_companies()
    {
        await using var context = TestDbContextFactory.Create();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        context.CanonicalTransactions.AddRange(
            Tx(companyA, "h1", reconciled: true),
            Tx(companyB, "h2", reconciled: false),
            Tx(companyB, "h3", reconciled: false));
        await context.SaveChangesAsync();

        var report = await Build(context).GetReconciliationRateAsync(companyA);

        Assert.Equal(1, report.TotalTransactions);
        Assert.Equal(100, report.OverallReconciliationRatePercent);
    }

    [Fact]
    public async Task Divergence_aging_groups_by_age_bucket()
    {
        await using var context = TestDbContextFactory.Create();
        var companyId = Guid.NewGuid();

        var recent = Tx(companyId, "h1", reconciled: false);
        var old = Tx(companyId, "h2", reconciled: false);
        context.CanonicalTransactions.AddRange(recent, old);
        context.Divergences.AddRange(
            new Divergence { TransactionId = recent.Id, Reason = DivergenceReason.NoCandidate, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Divergence { TransactionId = old.Id, Reason = DivergenceReason.NoCandidate, CreatedAt = DateTime.UtcNow.AddDays(-40) });
        await context.SaveChangesAsync();

        var report = await Build(context).GetDivergenceAgingAsync(companyId);

        Assert.Equal(1, report.Buckets.Single(b => b.Bucket == "0-3 dias").Count);
        Assert.Equal(1, report.Buckets.Single(b => b.Bucket == "31+ dias").Count);
        Assert.Equal(0, report.Buckets.Single(b => b.Bucket == "4-7 dias").Count);
    }

    [Fact]
    public async Task Audit_export_filters_by_period_and_orders_by_timestamp()
    {
        await using var context = TestDbContextFactory.Create();

        context.AuditEvents.AddRange(
            new AuditEvent { EntityType = "Source", Action = "Import", Timestamp = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
            new AuditEvent { EntityType = "Divergence", Action = "Created", Timestamp = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc) },
            new AuditEvent { EntityType = "Divergence", Action = "Assigned", Timestamp = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync();

        var entries = await Build(context).ExportAuditTrailAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(2, entries.Count);
        Assert.Equal("Import", entries[0].Action);
        Assert.Equal("Created", entries[1].Action);
    }

    [Fact]
    public async Task User_decision_history_returns_only_that_users_decisions()
    {
        await using var context = TestDbContextFactory.Create();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var transaction = Tx(Guid.NewGuid(), "h1", reconciled: false);
        var divergence = new Divergence { TransactionId = transaction.Id, Reason = DivergenceReason.NoCandidate };
        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);

        context.ApprovalDecisions.AddRange(
            new ApprovalDecision { DivergenceId = divergence.Id, UserId = userA, Decision = ApprovalDecisionType.ManualMatch, Notes = "a" },
            new ApprovalDecision { DivergenceId = divergence.Id, UserId = userB, Decision = ApprovalDecisionType.MarkNotReconcilable, Notes = "b" });
        await context.SaveChangesAsync();

        var history = await Build(context).GetUserDecisionHistoryAsync(userA);

        Assert.Single(history);
        Assert.Equal("ManualMatch", history[0].Decision);
        Assert.Equal("a", history[0].Notes);
    }
}
