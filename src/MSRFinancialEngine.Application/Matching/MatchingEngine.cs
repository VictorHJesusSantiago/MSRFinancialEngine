using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public interface IMatchingEngine
{
    Task<MatchingRunResult> RunForCompanyAsync(Guid companyId, CancellationToken ct = default);
}

public class MatchingRunResult
{
    public int TransactionsConsidered { get; set; }
    public int AutoApproved { get; set; }
    public int PendingReview { get; set; }
    public int DivergencesCreated { get; set; }
}

/// <summary>
/// Orquestra as estratégias de matching (determinística primeiro, depois fuzzy, na ordem
/// de prioridade das regras ativas da empresa) sobre o conjunto de transações não
/// reconciliadas. Transações que sobram sem candidato viram divergências.
/// </summary>
public class MatchingEngine : IMatchingEngine
{
    private const double AutoApproveThreshold = 0.98;

    private readonly IRepository<CanonicalTransaction> _transactionRepository;
    private readonly IRepository<MatchingRule> _ruleRepository;
    private readonly IRepository<MatchCandidate> _candidateRepository;
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IReadOnlyDictionary<MatchingRuleType, IMatchingStrategy> _strategies;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public MatchingEngine(
        IRepository<CanonicalTransaction> transactionRepository,
        IRepository<MatchingRule> ruleRepository,
        IRepository<MatchCandidate> candidateRepository,
        IRepository<Divergence> divergenceRepository,
        IEnumerable<IMatchingStrategy> strategies,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository;
        _ruleRepository = ruleRepository;
        _candidateRepository = candidateRepository;
        _divergenceRepository = divergenceRepository;
        _strategies = strategies.ToDictionary(s => s.Type);
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task<MatchingRunResult> RunForCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        var result = new MatchingRunResult();

        var rules = _ruleRepository.Query()
            .Where(r => r.CompanyId == companyId && r.Active)
            .OrderBy(r => r.Priority)
            .ToList();

        var unreconciled = _transactionRepository.Query()
            .Where(t => t.CompanyId == companyId && !t.Reconciled)
            .ToList();

        result.TransactionsConsidered = unreconciled.Count;

        var matchedIds = new HashSet<Guid>();
        var candidatesByTransaction = new Dictionary<Guid, List<MatchAttempt>>();

        foreach (var rule in rules)
        {
            if (!_strategies.TryGetValue(rule.Type, out var strategy))
                continue;

            var pool = unreconciled.Where(t => !matchedIds.Contains(t.Id)).ToList();
            var attempts = strategy.FindCandidates(pool, rule).OrderByDescending(a => a.Score).ToList();

            foreach (var attempt in attempts)
            {
                if (matchedIds.Contains(attempt.A.Id) || matchedIds.Contains(attempt.B.Id))
                    continue;

                if (attempt.Score >= AutoApproveThreshold)
                {
                    var candidate = new MatchCandidate
                    {
                        TransactionAId = attempt.A.Id,
                        TransactionBId = attempt.B.Id,
                        RuleId = rule.Id,
                        Score = attempt.Score,
                        Status = MatchCandidateStatus.AutoApproved
                    };
                    await _candidateRepository.AddAsync(candidate, ct);

                    attempt.A.Reconciled = true;
                    attempt.B.Reconciled = true;
                    _transactionRepository.Update(attempt.A);
                    _transactionRepository.Update(attempt.B);

                    matchedIds.Add(attempt.A.Id);
                    matchedIds.Add(attempt.B.Id);
                    result.AutoApproved++;

                    await _auditService.LogAsync(nameof(MatchCandidate), candidate.Id, "AutoApproved", null,
                        new { TransactionAId = attempt.A.Id, TransactionBId = attempt.B.Id, attempt.Score, Rule = rule.Name }, ct);
                }
                else
                {
                    Track(candidatesByTransaction, attempt.A.Id, attempt);
                    Track(candidatesByTransaction, attempt.B.Id, attempt);
                }
            }
        }

        // Transações que ainda não foram casadas: geram MatchCandidate pendente (se houver
        // algum candidato abaixo do limiar de auto-aprovação) e uma Divergence.
        foreach (var transaction in unreconciled.Where(t => !matchedIds.Contains(t.Id)))
        {
            candidatesByTransaction.TryGetValue(transaction.Id, out var attempts);
            var distinctAttempts = attempts?
                .GroupBy(a => a.A.Id == transaction.Id ? a.B.Id : a.A.Id)
                .Select(g => g.OrderByDescending(a => a.Score).First())
                .ToList() ?? new List<MatchAttempt>();

            foreach (var attempt in distinctAttempts)
            {
                var alreadyPersisted = _candidateRepository.Query().Any(c =>
                    (c.TransactionAId == attempt.A.Id && c.TransactionBId == attempt.B.Id) ||
                    (c.TransactionAId == attempt.B.Id && c.TransactionBId == attempt.A.Id));

                if (alreadyPersisted)
                    continue;

                var candidate = new MatchCandidate
                {
                    TransactionAId = attempt.A.Id,
                    TransactionBId = attempt.B.Id,
                    Score = attempt.Score,
                    Status = MatchCandidateStatus.PendingReview
                };
                await _candidateRepository.AddAsync(candidate, ct);
                result.PendingReview++;
            }

            var reason = distinctAttempts.Count == 0
                ? DivergenceReason.NoCandidate
                : distinctAttempts.Count > 1
                    ? DivergenceReason.MultipleCandidates
                    : DivergenceReason.AmountOutOfTolerance;

            var divergence = new Divergence
            {
                TransactionId = transaction.Id,
                Reason = reason,
                Status = DivergenceStatus.Open
            };
            await _divergenceRepository.AddAsync(divergence, ct);
            result.DivergencesCreated++;

            await _auditService.LogAsync(nameof(Divergence), divergence.Id, "Created", null,
                new { transaction.Id, Reason = reason.ToString() }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    private static void Track(Dictionary<Guid, List<MatchAttempt>> map, Guid transactionId, MatchAttempt attempt)
    {
        if (!map.TryGetValue(transactionId, out var list))
        {
            list = new List<MatchAttempt>();
            map[transactionId] = list;
        }
        list.Add(attempt);
    }
}
