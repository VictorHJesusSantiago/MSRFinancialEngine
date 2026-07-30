namespace MSRFinancialEngine.Domain.Entities;

public class MatchCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransactionAId { get; set; }
    public CanonicalTransaction? TransactionA { get; set; }

    public Guid TransactionBId { get; set; }
    public CanonicalTransaction? TransactionB { get; set; }

    public Guid? RuleId { get; set; }
    public MatchingRule? Rule { get; set; }

    /// <summary>Score de confiança de 0.0 a 1.0. 1.0 = match determinístico exato.</summary>
    public double Score { get; set; }

    public MatchCandidateStatus Status { get; set; } = MatchCandidateStatus.PendingReview;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
