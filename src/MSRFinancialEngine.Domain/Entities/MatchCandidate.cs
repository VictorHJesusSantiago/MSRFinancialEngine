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

    public double Score { get; set; }

    public MatchCandidateStatus Status { get; set; } = MatchCandidateStatus.PendingReview;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
