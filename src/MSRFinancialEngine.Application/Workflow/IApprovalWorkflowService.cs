using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Workflow;

public interface IApprovalWorkflowService
{
    Task AssignAsync(Guid divergenceId, Guid userId, CancellationToken ct = default);

    Task<Guid> DecideAsync(
        Guid divergenceId,
        Guid userId,
        ApprovalDecisionType decision,
        Guid? matchedTransactionId,
        string? notes,
        CancellationToken ct = default);
}
