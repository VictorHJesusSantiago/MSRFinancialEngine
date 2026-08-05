using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Observability;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Workflow;

public class ApprovalNotAuthorizedException : InvalidOperationException
{
    public ApprovalNotAuthorizedException(string message) : base(message)
    {
    }
}

public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IRepository<CanonicalTransaction> _transactionRepository;
    private readonly IRepository<ApprovalDecision> _decisionRepository;
    private readonly IRepository<MatchCandidate> _candidateRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IAuditService _auditService;
    private readonly EngineMetrics _metrics;
    private readonly IUnitOfWork _unitOfWork;

    public ApprovalWorkflowService(
        IRepository<Divergence> divergenceRepository,
        IRepository<CanonicalTransaction> transactionRepository,
        IRepository<ApprovalDecision> decisionRepository,
        IRepository<MatchCandidate> candidateRepository,
        IRepository<ApplicationUser> userRepository,
        IAuditService auditService,
        EngineMetrics metrics,
        IUnitOfWork unitOfWork)
    {
        _divergenceRepository = divergenceRepository;
        _transactionRepository = transactionRepository;
        _decisionRepository = decisionRepository;
        _candidateRepository = candidateRepository;
        _userRepository = userRepository;
        _auditService = auditService;
        _metrics = metrics;
        _unitOfWork = unitOfWork;
    }

    public async Task AssignAsync(Guid divergenceId, Guid userId, CancellationToken ct = default)
    {
        var divergence = await _divergenceRepository.GetByIdAsync(divergenceId, ct)
            ?? throw new InvalidOperationException($"Divergência '{divergenceId}' não encontrada.");

        var user = await GetActiveUserAsync(userId, ct);

        if (user.Role == UserRole.Viewer)
            throw new ApprovalNotAuthorizedException(
                $"Usuário '{user.Name}' tem papel {UserRole.Viewer} e não pode receber divergências para revisão.");

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

        var user = await GetActiveUserAsync(userId, ct);
        var transaction = await _transactionRepository.GetByIdAsync(divergence.TransactionId, ct)
            ?? throw new InvalidOperationException("Transação da divergência não encontrada.");

        EnsureCanApprove(user, transaction);

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

                var counterpart = await _transactionRepository.GetByIdAsync(matchedTransactionId.Value, ct)
                    ?? throw new InvalidOperationException("Transação casada informada não encontrada.");

                if (counterpart.CompanyId != transaction.CompanyId)
                    throw new InvalidOperationException("Não é possível casar transações de empresas diferentes.");

                if (counterpart.Id == transaction.Id)
                    throw new InvalidOperationException("Não é possível casar uma transação com ela mesma.");

                if (counterpart.Reconciled)
                    throw new InvalidOperationException("A transação informada já está reconciliada.");

                transaction.Reconciled = true;
                counterpart.Reconciled = true;
                _transactionRepository.Update(transaction);
                _transactionRepository.Update(counterpart);

                var suggested = _candidateRepository.Query().FirstOrDefault(c =>
                    (c.TransactionAId == transaction.Id && c.TransactionBId == counterpart.Id) ||
                    (c.TransactionAId == counterpart.Id && c.TransactionBId == transaction.Id));

                var score = decision == ApprovalDecisionType.AcceptSuggestion
                    ? suggested?.Score ?? 0.0
                    : 1.0;

                var candidate = new MatchCandidate
                {
                    TransactionAId = transaction.Id,
                    TransactionBId = counterpart.Id,
                    Score = score,
                    Status = MatchCandidateStatus.ManuallyApproved
                };
                await _candidateRepository.AddAsync(candidate, ct);

                RejectCompetingCandidates(transaction.Id, counterpart.Id);

                divergence.Status = DivergenceStatus.Resolved;
                divergence.ResolvedAt = DateTime.UtcNow;
                break;

            case ApprovalDecisionType.MarkNotReconcilable:
                RejectCompetingCandidates(transaction.Id, null);
                divergence.Status = DivergenceStatus.NotReconcilable;
                divergence.ResolvedAt = DateTime.UtcNow;
                break;
        }

        _divergenceRepository.Update(divergence);

        await _auditService.LogAsync(nameof(ApprovalDecision), approval.Id, decision.ToString(), userId,
            new { DivergenceId = divergence.Id, matchedTransactionId, notes }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _metrics.DecisionRecorded(decision.ToString());
        return approval.Id;
    }

    private void RejectCompetingCandidates(Guid transactionId, Guid? approvedCounterpartId)
    {
        var pending = _candidateRepository.Query()
            .Where(c => c.Status == MatchCandidateStatus.PendingReview
                        && (c.TransactionAId == transactionId || c.TransactionBId == transactionId))
            .ToList();

        foreach (var candidate in pending)
        {
            var counterpartId = candidate.TransactionAId == transactionId
                ? candidate.TransactionBId
                : candidate.TransactionAId;

            if (approvedCounterpartId is not null && counterpartId == approvedCounterpartId)
                continue;

            candidate.Status = MatchCandidateStatus.Rejected;
            _candidateRepository.Update(candidate);
        }
    }

    private async Task<ApplicationUser> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"Usuário '{userId}' não encontrado.");

        if (!user.Active)
            throw new ApprovalNotAuthorizedException($"Usuário '{user.Name}' está inativo.");

        return user;
    }

    private static void EnsureCanApprove(ApplicationUser user, CanonicalTransaction transaction)
    {
        if (user.Role is UserRole.Viewer or UserRole.Analyst)
            throw new ApprovalNotAuthorizedException(
                $"Papel {user.Role} não pode decidir divergências; é necessário {UserRole.Approver} ou {UserRole.Admin}.");

        if (user.Role == UserRole.Admin || user.ApprovalLimitAmount is null)
            return;

        var amount = Math.Abs(transaction.Amount);
        if (amount > user.ApprovalLimitAmount.Value)
            throw new ApprovalNotAuthorizedException(
                $"Valor {amount:N2} excede a alçada de {user.ApprovalLimitAmount.Value:N2} do usuário '{user.Name}'. " +
                "Encaminhe para um aprovador com alçada maior.");
    }
}
