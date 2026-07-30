using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Workflow;

/// <summary>
/// Fila de revisão manual: atribuição de divergências a usuários e registro da decisão
/// (aceitar sugestão, match manual, ou marcar como não-reconciliável), com auditoria
/// de quem decidiu e quando.
/// </summary>
public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IRepository<CanonicalTransaction> _transactionRepository;
    private readonly IRepository<ApprovalDecision> _decisionRepository;
    private readonly IRepository<MatchCandidate> _candidateRepository;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public ApprovalWorkflowService(
        IRepository<Divergence> divergenceRepository,
        IRepository<CanonicalTransaction> transactionRepository,
        IRepository<ApprovalDecision> decisionRepository,
        IRepository<MatchCandidate> candidateRepository,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _divergenceRepository = divergenceRepository;
        _transactionRepository = transactionRepository;
        _decisionRepository = decisionRepository;
        _candidateRepository = candidateRepository;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task AssignAsync(Guid divergenceId, Guid userId, CancellationToken ct = default)
    {
        var divergence = await _divergenceRepository.GetByIdAsync(divergenceId, ct)
            ?? throw new InvalidOperationException($"Divergência '{divergenceId}' não encontrada.");

        divergence.AssignedToUserId = userId;
        divergence.Status = DivergenceStatus.InReview;
        _divergenceRepository.Update(divergence);

        await _auditService.LogAsync(nameof(Divergence), divergence.Id, "Assigned", userId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<Guid> DecideAsync(
        Guid divergenceId,
        Guid userId,
        ApprovalDecisionType decision,
        Guid? matchedTransactionId,
        string? notes,
        CancellationToken ct = default)
    {
        var divergence = await _divergenceRepository.GetByIdAsync(divergenceId, ct)
            ?? throw new InvalidOperationException($"Divergência '{divergenceId}' não encontrada.");

        if (divergence.Status is DivergenceStatus.Resolved or DivergenceStatus.NotReconcilable)
            throw new InvalidOperationException("Esta divergência já foi finalizada.");

        var approval = new ApprovalDecision
        {
            DivergenceId = divergence.Id,
            UserId = userId,
            Decision = decision,
            MatchedTransactionId = matchedTransactionId,
            Notes = notes
        };
        await _decisionRepository.AddAsync(approval, ct);

        switch (decision)
        {
            case ApprovalDecisionType.AcceptSuggestion:
            case ApprovalDecisionType.ManualMatch:
                if (matchedTransactionId is null)
                    throw new InvalidOperationException("É necessário informar a transação casada para esta decisão.");

                var transactionA = await _transactionRepository.GetByIdAsync(divergence.TransactionId, ct)
                    ?? throw new InvalidOperationException("Transação da divergência não encontrada.");
                var transactionB = await _transactionRepository.GetByIdAsync(matchedTransactionId.Value, ct)
                    ?? throw new InvalidOperationException("Transação casada informada não encontrada.");

                transactionA.Reconciled = true;
                transactionB.Reconciled = true;
                _transactionRepository.Update(transactionA);
                _transactionRepository.Update(transactionB);

                var candidate = new MatchCandidate
                {
                    TransactionAId = transactionA.Id,
                    TransactionBId = transactionB.Id,
                    Score = decision == ApprovalDecisionType.AcceptSuggestion ? 0.0 : 1.0,
                    Status = MatchCandidateStatus.ManuallyApproved
                };
                await _candidateRepository.AddAsync(candidate, ct);

                divergence.Status = DivergenceStatus.Resolved;
                divergence.ResolvedAt = DateTime.UtcNow;
                break;

            case ApprovalDecisionType.MarkNotReconcilable:
                divergence.Status = DivergenceStatus.NotReconcilable;
                divergence.ResolvedAt = DateTime.UtcNow;
                break;
        }

        _divergenceRepository.Update(divergence);

        await _auditService.LogAsync(nameof(ApprovalDecision), approval.Id, decision.ToString(), userId,
            new { divergence.Id, matchedTransactionId, notes }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return approval.Id;
    }
}
