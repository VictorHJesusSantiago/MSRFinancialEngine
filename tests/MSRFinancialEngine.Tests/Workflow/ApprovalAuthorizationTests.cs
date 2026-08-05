using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Workflow;

public class ApprovalAuthorizationTests
{
    private static ApprovalWorkflowService Build(FinancialEngineDbContext context) => new(
        new EfRepository<Divergence>(context),
        new EfRepository<CanonicalTransaction>(context),
        new EfRepository<ApprovalDecision>(context),
        new EfRepository<MatchCandidate>(context),
        new EfRepository<ApplicationUser>(context),
        new AuditService(new EfRepository<AuditEvent>(context)),
        TestMetrics.Create(),
        new EfUnitOfWork(context));

    private static async Task<(FinancialEngineDbContext ctx, Divergence div, CanonicalTransaction tx)> SeedAsync(decimal amount)
    {
        var context = TestDbContextFactory.Create();
        var transaction = new CanonicalTransaction
        {
            CompanyId = Guid.NewGuid(),
            SourceId = Guid.NewGuid(),
            Amount = amount,
            CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10),
            Description = "TESTE",
            Hash = Guid.NewGuid().ToString()
        };
        var divergence = new Divergence { TransactionId = transaction.Id, Reason = DivergenceReason.NoCandidate };

        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        return (context, divergence, transaction);
    }

    private static async Task<ApplicationUser> AddUserAsync(
        FinancialEngineDbContext context, UserRole role, decimal? limit = null, bool active = true)
    {
        var user = new ApplicationUser
        {
            Name = role.ToString(),
            Email = $"{Guid.NewGuid()}@x.com",
            Role = role,
            ApprovalLimitAmount = limit,
            Active = active
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.Analyst)]
    public async Task Roles_below_approver_cannot_decide(UserRole role)
    {
        var (context, divergence, _) = await SeedAsync(100m);
        await using var _ctx = context;
        var user = await AddUserAsync(context, role);

        await Assert.ThrowsAsync<ApprovalNotAuthorizedException>(() =>
            Build(context).DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.MarkNotReconcilable, null, null));
    }

    [Fact]
    public async Task Viewer_cannot_even_be_assigned_a_divergence()
    {
        var (context, divergence, _) = await SeedAsync(100m);
        await using var _ctx = context;
        var viewer = await AddUserAsync(context, UserRole.Viewer);

        await Assert.ThrowsAsync<ApprovalNotAuthorizedException>(() =>
            Build(context).AssignAsync(divergence.Id, viewer.Id));
    }

    [Fact]
    public async Task Analyst_can_be_assigned_for_investigation()
    {
        var (context, divergence, _) = await SeedAsync(100m);
        await using var _ctx = context;
        var analyst = await AddUserAsync(context, UserRole.Analyst);

        await Build(context).AssignAsync(divergence.Id, analyst.Id);

        await context.Entry(divergence).ReloadAsync();
        Assert.Equal(DivergenceStatus.InReview, divergence.Status);
    }

    [Fact]
    public async Task Approver_within_limit_can_decide()
    {
        var (context, divergence, _) = await SeedAsync(500m);
        await using var _ctx = context;
        var approver = await AddUserAsync(context, UserRole.Approver, limit: 1000m);

        await Build(context).DecideAsync(divergence.Id, approver.Id, ApprovalDecisionType.MarkNotReconcilable, null, null);

        await context.Entry(divergence).ReloadAsync();
        Assert.Equal(DivergenceStatus.NotReconcilable, divergence.Status);
    }

    [Fact]
    public async Task Approver_above_limit_is_blocked()
    {
        var (context, divergence, _) = await SeedAsync(5000m);
        await using var _ctx = context;
        var approver = await AddUserAsync(context, UserRole.Approver, limit: 1000m);

        var ex = await Assert.ThrowsAsync<ApprovalNotAuthorizedException>(() =>
            Build(context).DecideAsync(divergence.Id, approver.Id, ApprovalDecisionType.MarkNotReconcilable, null, null));

        Assert.Contains("alçada", ex.Message);
    }

    [Fact]
    public async Task Limit_applies_to_the_magnitude_so_debits_are_covered()
    {
        var (context, divergence, _) = await SeedAsync(-5000m);
        await using var _ctx = context;
        var approver = await AddUserAsync(context, UserRole.Approver, limit: 1000m);

        await Assert.ThrowsAsync<ApprovalNotAuthorizedException>(() =>
            Build(context).DecideAsync(divergence.Id, approver.Id, ApprovalDecisionType.MarkNotReconcilable, null, null));
    }

    [Fact]
    public async Task Admin_has_no_limit()
    {
        var (context, divergence, _) = await SeedAsync(9_999_999m);
        await using var _ctx = context;
        var admin = await AddUserAsync(context, UserRole.Admin, limit: 10m);

        await Build(context).DecideAsync(divergence.Id, admin.Id, ApprovalDecisionType.MarkNotReconcilable, null, null);

        await context.Entry(divergence).ReloadAsync();
        Assert.Equal(DivergenceStatus.NotReconcilable, divergence.Status);
    }

    [Fact]
    public async Task Approver_without_limit_is_unlimited()
    {
        var (context, divergence, _) = await SeedAsync(9_999_999m);
        await using var _ctx = context;
        var approver = await AddUserAsync(context, UserRole.Approver, limit: null);

        await Build(context).DecideAsync(divergence.Id, approver.Id, ApprovalDecisionType.MarkNotReconcilable, null, null);

        await context.Entry(divergence).ReloadAsync();
        Assert.Equal(DivergenceStatus.NotReconcilable, divergence.Status);
    }

    [Fact]
    public async Task Inactive_user_cannot_decide()
    {
        var (context, divergence, _) = await SeedAsync(100m);
        await using var _ctx = context;
        var user = await AddUserAsync(context, UserRole.Admin, active: false);

        await Assert.ThrowsAsync<ApprovalNotAuthorizedException>(() =>
            Build(context).DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.MarkNotReconcilable, null, null));
    }

    [Fact]
    public async Task Cannot_manually_match_across_companies()
    {
        var (context, divergence, transaction) = await SeedAsync(100m);
        await using var _ctx = context;
        var admin = await AddUserAsync(context, UserRole.Admin);

        var otherCompanyTx = new CanonicalTransaction
        {
            CompanyId = Guid.NewGuid(),
            SourceId = Guid.NewGuid(),
            Amount = 100m,
            CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10),
            Description = "OUTRA EMPRESA",
            Hash = "outro"
        };
        context.CanonicalTransactions.Add(otherCompanyTx);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Build(context).DecideAsync(divergence.Id, admin.Id, ApprovalDecisionType.ManualMatch, otherCompanyTx.Id, null));

        Assert.Contains("empresas diferentes", ex.Message);
    }

    [Fact]
    public async Task Cannot_match_a_transaction_with_itself()
    {
        var (context, divergence, transaction) = await SeedAsync(100m);
        await using var _ctx = context;
        var admin = await AddUserAsync(context, UserRole.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Build(context).DecideAsync(divergence.Id, admin.Id, ApprovalDecisionType.ManualMatch, transaction.Id, null));
    }

    [Fact]
    public async Task Competing_pending_candidates_are_rejected_after_the_decision()
    {
        var (context, divergence, transaction) = await SeedAsync(100m);
        await using var _ctx = context;
        var admin = await AddUserAsync(context, UserRole.Admin);

        var chosen = new CanonicalTransaction
        {
            CompanyId = transaction.CompanyId, SourceId = Guid.NewGuid(), Amount = 100m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "ESCOLHIDA", Hash = "c1"
        };
        var rejected = new CanonicalTransaction
        {
            CompanyId = transaction.CompanyId, SourceId = Guid.NewGuid(), Amount = 100m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "DESCARTADA", Hash = "c2"
        };
        context.CanonicalTransactions.AddRange(chosen, rejected);

        var chosenCandidate = new MatchCandidate
        {
            TransactionAId = transaction.Id, TransactionBId = chosen.Id,
            Score = 0.8, Status = MatchCandidateStatus.PendingReview
        };
        var losingCandidate = new MatchCandidate
        {
            TransactionAId = transaction.Id, TransactionBId = rejected.Id,
            Score = 0.7, Status = MatchCandidateStatus.PendingReview
        };
        context.MatchCandidates.AddRange(chosenCandidate, losingCandidate);
        await context.SaveChangesAsync();

        await Build(context).DecideAsync(divergence.Id, admin.Id, ApprovalDecisionType.AcceptSuggestion, chosen.Id, null);

        await context.Entry(losingCandidate).ReloadAsync();
        await context.Entry(chosenCandidate).ReloadAsync();

        Assert.Equal(MatchCandidateStatus.Rejected, losingCandidate.Status);
        Assert.Equal(MatchCandidateStatus.PendingReview, chosenCandidate.Status);
    }
}
