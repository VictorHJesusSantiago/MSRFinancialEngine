using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Reports;

public class ReportService : IReportService
{
    private readonly IRepository<CanonicalTransaction> _transactionRepository;
    private readonly IRepository<MatchCandidate> _candidateRepository;
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IRepository<ApprovalDecision> _decisionRepository;
    private readonly IRepository<AuditEvent> _auditRepository;

    public ReportService(
        IRepository<CanonicalTransaction> transactionRepository,
        IRepository<MatchCandidate> candidateRepository,
        IRepository<Divergence> divergenceRepository,
        IRepository<ApprovalDecision> decisionRepository,
        IRepository<AuditEvent> auditRepository)
    {
        _transactionRepository = transactionRepository;
        _candidateRepository = candidateRepository;
        _divergenceRepository = divergenceRepository;
        _decisionRepository = decisionRepository;
        _auditRepository = auditRepository;
    }

    public Task<ReconciliationRateReport> GetReconciliationRateAsync(Guid companyId, CancellationToken ct = default)
    {
        var transactions = _transactionRepository.Query().Where(t => t.CompanyId == companyId).ToList();
        var total = transactions.Count;
        var reconciled = transactions.Count(t => t.Reconciled);

        var reconciledIds = transactions.Where(t => t.Reconciled).Select(t => t.Id).ToHashSet();
        var autoApprovedCount = _candidateRepository.Query()
            .Where(c => c.Status == MatchCandidateStatus.AutoApproved)
            .Count(c => reconciledIds.Contains(c.TransactionAId) || reconciledIds.Contains(c.TransactionBId));

        var report = new ReconciliationRateReport
        {
            CompanyId = companyId,
            TotalTransactions = total,
            Reconciled = reconciled,
            Pending = total - reconciled,
            OverallReconciliationRatePercent = total == 0 ? 0 : Math.Round(reconciled * 100.0 / total, 2),
            AutoReconciliationRatePercent = total == 0 ? 0 : Math.Round(autoApprovedCount * 100.0 / total, 2)
        };

        return Task.FromResult(report);
    }

    public Task<DivergenceAgingReport> GetDivergenceAgingAsync(Guid companyId, CancellationToken ct = default)
    {
        var openDivergences = (from d in _divergenceRepository.Query()
                                join t in _transactionRepository.Query() on d.TransactionId equals t.Id
                                where t.CompanyId == companyId && d.Status != DivergenceStatus.Resolved
                                select d).ToList();

        var now = DateTime.UtcNow;
        var buckets = new[] { "0-3 dias", "4-7 dias", "8-15 dias", "16-30 dias", "31+ dias" };
        var counts = new int[buckets.Length];

        foreach (var d in openDivergences)
        {
            var ageDays = (now - d.CreatedAt).TotalDays;
            var idx = ageDays switch
            {
                <= 3 => 0,
                <= 7 => 1,
                <= 15 => 2,
                <= 30 => 3,
                _ => 4
            };
            counts[idx]++;
        }

        var report = new DivergenceAgingReport
        {
            CompanyId = companyId,
            Buckets = buckets.Select((b, i) => new DivergenceAgingBucket { Bucket = b, Count = counts[i] }).ToList()
        };

        return Task.FromResult(report);
    }

    public Task<List<UserDecisionHistoryEntry>> GetUserDecisionHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var history = _decisionRepository.Query()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.DecidedAt)
            .Select(d => new UserDecisionHistoryEntry
            {
                DivergenceId = d.DivergenceId,
                Decision = d.Decision.ToString(),
                DecidedAt = d.DecidedAt,
                Notes = d.Notes
            })
            .ToList();

        return Task.FromResult(history);
    }

    public Task<List<AuditExportEntry>> ExportAuditTrailAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var entries = _auditRepository.Query()
            .Where(e => e.Timestamp >= fromUtc && e.Timestamp <= toUtc)
            .OrderBy(e => e.Timestamp)
            .Select(e => new AuditExportEntry
            {
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                Action = e.Action,
                UserId = e.UserId,
                Timestamp = e.Timestamp,
                DetailsJson = e.DetailsJson
            })
            .ToList();

        return Task.FromResult(entries);
    }
}
