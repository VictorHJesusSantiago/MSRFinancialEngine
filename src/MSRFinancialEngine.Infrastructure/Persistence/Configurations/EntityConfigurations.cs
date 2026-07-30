using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TaxId).HasMaxLength(32);
        builder.Property(c => c.BaseCurrencyCode).HasMaxLength(3);
        builder.HasIndex(c => c.TaxId);
    }
}

public class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("sources");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.HasOne(s => s.Company).WithMany(c => c.Sources).HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.CompanyId);
    }
}

public class RawTransactionConfiguration : IEntityTypeConfiguration<RawTransaction>
{
    public void Configure(EntityTypeBuilder<RawTransaction> builder)
    {
        builder.ToTable("raw_transactions");
        builder.HasKey(r => r.Id);
        builder.HasOne(r => r.Source).WithMany(s => s.RawTransactions).HasForeignKey(r => r.SourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.SourceId);
    }
}

public class CanonicalTransactionConfiguration : IEntityTypeConfiguration<CanonicalTransaction>
{
    public void Configure(EntityTypeBuilder<CanonicalTransaction> builder)
    {
        builder.ToTable("canonical_transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount).HasColumnType("numeric(18,2)");
        builder.Property(t => t.CurrencyCode).HasMaxLength(3);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Hash).HasMaxLength(64);
        // Data de negócio sem fuso horário (vem de extratos/ERPs sem timezone) — não usar timestamptz.
        builder.Property(t => t.TransactionDate).HasColumnType("timestamp without time zone");

        builder.HasOne(t => t.Company).WithMany(c => c.Transactions).HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.RawTransaction).WithOne(r => r.CanonicalTransaction)
            .HasForeignKey<CanonicalTransaction>(t => t.RawTransactionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.CompanyId, t.Hash });
        builder.HasIndex(t => new { t.CompanyId, t.Reconciled });
    }
}

public class MatchingRuleConfiguration : IEntityTypeConfiguration<MatchingRule>
{
    public void Configure(EntityTypeBuilder<MatchingRule> builder)
    {
        builder.ToTable("matching_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.HasOne(r => r.Company).WithMany(c => c.MatchingRules).HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.CompanyId, r.Active, r.Priority });
    }
}

public class MatchCandidateConfiguration : IEntityTypeConfiguration<MatchCandidate>
{
    public void Configure(EntityTypeBuilder<MatchCandidate> builder)
    {
        builder.ToTable("match_candidates");
        builder.HasKey(m => m.Id);

        builder.HasOne(m => m.TransactionA).WithMany(t => t.MatchCandidatesAsA)
            .HasForeignKey(m => m.TransactionAId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.TransactionB).WithMany(t => t.MatchCandidatesAsB)
            .HasForeignKey(m => m.TransactionBId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Rule).WithMany(r => r.MatchCandidates)
            .HasForeignKey(m => m.RuleId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.TransactionAId);
        builder.HasIndex(m => m.TransactionBId);
    }
}

public class DivergenceConfiguration : IEntityTypeConfiguration<Divergence>
{
    public void Configure(EntityTypeBuilder<Divergence> builder)
    {
        builder.ToTable("divergences");
        builder.HasKey(d => d.Id);

        builder.HasOne(d => d.Transaction).WithOne(t => t.Divergence)
            .HasForeignKey<Divergence>(d => d.TransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.AssignedTo).WithMany().HasForeignKey(d => d.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.Status);
    }
}

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("approval_decisions");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Divergence).WithMany(d => d.Decisions).HasForeignKey(a => a.DivergenceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.UserId);
    }
}

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).HasMaxLength(200);
        builder.Property(a => a.Action).HasMaxLength(100);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.Timestamp);
    }
}

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CurrencyCode).HasMaxLength(3);
        builder.Property(e => e.BaseCurrencyCode).HasMaxLength(3);
        builder.Property(e => e.RateToBase).HasColumnType("numeric(18,6)");
        builder.HasIndex(e => new { e.CurrencyCode, e.BaseCurrencyCode, e.Date }).IsUnique();
    }
}

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
