namespace MSRFinancialEngine.Domain.Entities;

public class Divergence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public CanonicalTransaction? Transaction { get; set; }

    public DivergenceReason Reason { get; set; }
    public DivergenceStatus Status { get; set; } = DivergenceStatus.Open;
    public Guid? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();
}
