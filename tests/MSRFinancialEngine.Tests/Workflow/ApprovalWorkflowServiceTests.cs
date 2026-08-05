using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Workflow;

public class ApprovalWorkflowServiceTests
{
    private static (ApprovalWorkflowService service, FinancialEngineDbContext context) Build()
    {
        var context = TestDbContextFactory.Create();
        var service = new ApprovalWorkflowService(
            new EfRepository<Divergence>(context),
            new EfRepository<CanonicalTransaction>(context),
            new EfRepository<ApprovalDecision>(context),
            new EfRepository<MatchCandidate>(context),
            new EfRepository<ApplicationUser>(context),
            new AuditService(new EfRepository<AuditEvent>(context)),
            TestMetrics.Create(),
            new EfUnitOfWork(context));

        return (service, context);
    }

    private static CanonicalTransaction Tx(Guid companyId, string hash) => new()
    {
        CompanyId = companyId,
        SourceId = Guid.NewGuid(),
        Amount = 100m,
        CurrencyCode = "BRL",
        TransactionDate = new DateTime(2026, 1, 10),
        Description = "TESTE",
        Hash = hash
    };

    [Fact]
    public async Task Assign_sets_user_and_moves_to_in_review()
    {
        var (service, context) = Build();
        await using var _ = context;

        var companyId = Guid.NewGuid();
        var user = new ApplicationUser { Name = "Ana", Email = "ana@x.com", Role = UserRole.Approver };
        var transaction = Tx(companyId, "h1");
        var divergence = new Divergence { TransactionId = transaction.Id, Reason = DivergenceReason.NoCandidate };

        context.Users.Add(user);
        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        await service.AssignAsync(divergence.Id, user.Id);

        await context.Entry(divergence).ReloadAsync();
        Assert.Equal(user.Id, divergence.AssignedToUserId);
        Assert.Equal(DivergenceStatus.InReview, divergence.Status);
    }

    [Fact]
    public async Task Manual_match_reconciles_both_transactions_and_resolves_divergence()
    {
        var (service, context) = Build();
        await using var _ = context;

        var companyId = Guid.NewGuid();
        var user = new ApplicationUser { Name = "Ana", Email = "ana@x.com", Role = UserRole.Approver };
        var a = Tx(companyId, "h1");
        var b = Tx(companyId, "h2");
        var divergence = new Divergence { TransactionId = a.Id, Reason = DivergenceReason.NoCandidate };

        context.Users.Add(user);
        context.CanonicalTransactions.AddRange(a, b);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        await service.DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.ManualMatch, b.Id, "casado manualmente");

        await context.Entry(a).ReloadAsync();
        await context.Entry(b).ReloadAsync();
        await context.Entry(divergence).ReloadAsync();

        Assert.True(a.Reconciled);
        Assert.True(b.Reconciled);
        Assert.Equal(DivergenceStatus.Resolved, divergence.Status);
        Assert.NotNull(divergence.ResolvedAt);

        var candidate = context.MatchCandidates.Single();
        Assert.Equal(MatchCandidateStatus.ManuallyApproved, candidate.Status);
    }

    [Fact]
    public async Task Mark_not_reconcilable_closes_divergence_without_matching()
    {
        var (service, context) = Build();
        await using var _ = context;

        var user = new ApplicationUser { Name = "Ana", Email = "ana@x.com", Role = UserRole.Approver };
        var transaction = Tx(Guid.NewGuid(), "h1");
        var divergence = new Divergence { TransactionId = transaction.Id, Reason = DivergenceReason.NoCandidate };

        context.Users.Add(user);
        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        await service.DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.MarkNotReconcilable, null, "tarifa isolada");

        await context.Entry(transaction).ReloadAsync();
        await context.Entry(divergence).ReloadAsync();

        Assert.False(transaction.Reconciled);
        Assert.Equal(DivergenceStatus.NotReconcilable, divergence.Status);
        Assert.Empty(context.MatchCandidates);
    }

    [Fact]
    public async Task Manual_match_without_target_transaction_is_rejected()
    {
        var (service, context) = Build();
        await using var _ = context;

        var user = new ApplicationUser { Name = "Ana", Email = "ana@x.com", Role = UserRole.Approver };
        var transaction = Tx(Guid.NewGuid(), "h1");
        var divergence = new Divergence { TransactionId = transaction.Id, Reason = DivergenceReason.NoCandidate };

        context.Users.Add(user);
        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.ManualMatch, null, null));
    }

    [Fact]
    public async Task Deciding_an_already_finalized_divergence_is_rejected()
    {
        var (service, context) = Build();
        await using var _ = context;

        var user = new ApplicationUser { Name = "Ana", Email = "ana@x.com", Role = UserRole.Approver };
        var transaction = Tx(Guid.NewGuid(), "h1");
        var divergence = new Divergence
        {
            TransactionId = transaction.Id,
            Reason = DivergenceReason.NoCandidate,
            Status = DivergenceStatus.Resolved
        };

        context.Users.Add(user);
        context.CanonicalTransactions.Add(transaction);
        context.Divergences.Add(divergence);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsync(divergence.Id, user.Id, ApprovalDecisionType.MarkNotReconcilable, null, null));
    }
}
