using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Infrastructure.Persistence;

public class FinancialEngineDbContext : DbContext
{
    public FinancialEngineDbContext(DbContextOptions<FinancialEngineDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RawTransaction> RawTransactions => Set<RawTransaction>();
    public DbSet<CanonicalTransaction> CanonicalTransactions => Set<CanonicalTransaction>();
    public DbSet<MatchingRule> MatchingRules => Set<MatchingRule>();
    public DbSet<MatchCandidate> MatchCandidates => Set<MatchCandidate>();
    public DbSet<Divergence> Divergences => Set<Divergence>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancialEngineDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
