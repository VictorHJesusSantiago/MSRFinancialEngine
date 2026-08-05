using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Infrastructure.Persistence;

public class FinancialEngineDbContext : DbContext
{
    private readonly ICompanyContext? _companyContext;

    public FinancialEngineDbContext(DbContextOptions<FinancialEngineDbContext> options) : base(options)
    {
    }

    public FinancialEngineDbContext(DbContextOptions<FinancialEngineDbContext> options, ICompanyContext companyContext)
        : base(options)
    {
        _companyContext = companyContext;
    }

    private Guid? ActiveCompanyId => _companyContext?.CompanyId;

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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<AuditArchive> AuditArchives => Set<AuditArchive>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancialEngineDbContext).Assembly);

        modelBuilder.Entity<Source>()
            .HasQueryFilter(s => ActiveCompanyId == null || s.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<CanonicalTransaction>()
            .HasQueryFilter(t => ActiveCompanyId == null || t.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<MatchingRule>()
            .HasQueryFilter(r => ActiveCompanyId == null || r.CompanyId == ActiveCompanyId);

        modelBuilder.Entity<Divergence>()
            .HasQueryFilter(d => ActiveCompanyId == null || d.Transaction!.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<RawTransaction>()
            .HasQueryFilter(r => ActiveCompanyId == null || r.Source!.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<ImportJob>()
            .HasQueryFilter(j => ActiveCompanyId == null || j.Source!.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<MatchCandidate>()
            .HasQueryFilter(c => ActiveCompanyId == null || c.TransactionA!.CompanyId == ActiveCompanyId);
        modelBuilder.Entity<ApprovalDecision>()
            .HasQueryFilter(a => ActiveCompanyId == null || a.Divergence!.Transaction!.CompanyId == ActiveCompanyId);

        base.OnModelCreating(modelBuilder);
    }
}
