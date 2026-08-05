using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Application.Observability;
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

    public int MissingExchangeRates { get; set; }
}

public class MatchingEngine : IMatchingEngine
{
    private const double AutoApproveThreshold = 0.98;

    private readonly IRepository<CanonicalTransaction> _transactionRepository;
    private readonly IRepository<MatchingRule> _ruleRepository;
    private readonly IRepository<MatchCandidate> _candidateRepository;
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly IReadOnlyDictionary<MatchingRuleType, IMatchingStrategy> _strategies;
    private readonly ICurrencyConversionService _currencyConversionService;
    private readonly IMatchingRunGuard _runGuard;
    private readonly EngineMetrics _metrics;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public MatchingEngine(
        IRepository<CanonicalTransaction> transactionRepository,
        IRepository<MatchingRule> ruleRepository,
        IRepository<MatchCandidate> candidateRepository,
        IRepository<Divergence> divergenceRepository,
        IRepository<Company> companyRepository,
        IEnumerable<IMatchingStrategy> strategies,
        ICurrencyConversionService currencyConversionService,
        IMatchingRunGuard runGuard,
        EngineMetrics metrics,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository;
        _ruleRepository = ruleRepository;
        _candidateRepository = candidateRepository;
        _divergenceRepository = divergenceRepository;
        _companyRepository = companyRepository;
        _strategies = strategies.ToDictionary(s => s.Type);
        _currencyConversionService = currencyConversionService;
        _runGuard = runGuard;
        _metrics = metrics;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task<MatchingRunResult> RunForCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var runLock = await _runGuard.TryAcquireAsync(companyId, ct)
            ?? throw new MatchingAlreadyRunningException(companyId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await RunInternalAsync(companyId, ct);
        stopwatch.Stop();

        _metrics.MatchingCompleted(companyId, result.AutoApproved, result.DivergencesCreated,
            result.MissingExchangeRates, stopwatch.Elapsed.TotalMilliseconds);

        return result;
    }

    private async Task<MatchingRunResult> RunInternalAsync(Guid companyId, CancellationToken ct)
    {
        var result = new MatchingRunResult();

        var company = await _companyRepository.GetByIdAsync(companyId, ct)
            ?? throw new InvalidOperationException($"Empresa '{companyId}' não encontrada.");

        var rules = _ruleRepository.Query()
            .Where(r => r.CompanyId == companyId && r.Active)
            .OrderBy(r => r.Priority)
            .ToList();

        var unreconciled = _transactionRepository.Query()
            .Where(t => t.CompanyId == companyId && !t.Reconciled)
            .ToList();

        result.TransactionsConsidered = unreconciled.Count;

        var baseAmounts = await ResolveBaseAmountsAsync(unreconciled, company.BaseCurrencyCode, ct);
        result.MissingExchangeRates = baseAmounts.Count(kv => kv.Value is null);

        var context = new MatchingContext(unreconciled, baseAmounts, company.BaseCurrencyCode);

        var matchedIds = new HashSet<Guid>();
        var candidatesByTransaction = new Dictionary<Guid, List<MatchAttempt>>();

        foreach (var rule in rules)
        {
            if (!_strategies.TryGetValue(rule.Type, out var strategy))
                continue;

            var pool = unreconciled.Where(t => !matchedIds.Contains(t.Id)).ToList();
            var attempts = strategy.FindCandidates(context.WithPool(pool), rule)
                .OrderByDescending(a => a.Score)
                .ToList();

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

            var reason = DivergenceReasonAnalyzer.Analyze(transaction, context, distinctAttempts);

            var divergence = new Divergence
            {
                TransactionId = transaction.Id,
                Reason = reason,
                Status = DivergenceStatus.Open
            };
            await _divergenceRepository.AddAsync(divergence, ct);
            result.DivergencesCreated++;

            await _auditService.LogAsync(nameof(Divergence), divergence.Id, "Created", null,
                new { TransactionId = transaction.Id, Reason = reason.ToString() }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<Guid, decimal?>> ResolveBaseAmountsAsync(
        IReadOnlyList<CanonicalTransaction> transactions, string baseCurrencyCode, CancellationToken ct)
    {
        var baseAmounts = new Dictionary<Guid, decimal?>(transactions.Count);

        foreach (var transaction in transactions)
        {
            if (string.Equals(transaction.CurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                baseAmounts[transaction.Id] = transaction.Amount;
                continue;
            }

            try
            {
                baseAmounts[transaction.Id] = await _currencyConversionService.ConvertToBaseAsync(
                    transaction.Amount,
                    transaction.CurrencyCode,
                    baseCurrencyCode,
                    DateOnly.FromDateTime(transaction.TransactionDate),
                    ct);
            }
            catch (InvalidOperationException)
            {
                baseAmounts[transaction.Id] = null;
            }
        }

        return baseAmounts;
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
