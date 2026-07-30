namespace MSRFinancialEngine.Domain.Entities;

public class ApprovalDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DivergenceId { get; set; }
    public Divergence? Divergence { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public ApprovalDecisionType Decision { get; set; }
    public Guid? MatchedTransactionId { get; set; }
    public string? Notes { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
