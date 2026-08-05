using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Retention;

public class RetentionService : IRetentionService
{
    private readonly IRepository<RefreshToken> _refreshTokens;
    private readonly IRepository<ImportJob> _importJobs;
    private readonly IRepository<AuditEvent> _auditEvents;
    private readonly IRepository<AuditArchive> _auditArchives;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RetentionOptions _options;

    public RetentionService(
        IRepository<RefreshToken> refreshTokens,
        IRepository<ImportJob> importJobs,
        IRepository<AuditEvent> auditEvents,
        IRepository<AuditArchive> auditArchives,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        RetentionOptions options)
    {
        _refreshTokens = refreshTokens;
        _importJobs = importJobs;
        _auditEvents = auditEvents;
        _auditArchives = auditArchives;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    public async Task<RetentionResult> PurgeAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var result = new RetentionResult();

        result.RefreshTokensRemoved = PurgeRefreshTokens(now);
        result.ImportJobsRemoved = PurgeImportJobs(now);
        result.AuditEventsRemoved = PurgeAuditEvents(now);

        if (result.RefreshTokensRemoved + result.ImportJobsRemoved + result.AuditEventsRemoved > 0)
        {
            await _auditService.LogAsync(nameof(RetentionService), Guid.Empty, "Purged", null, new
            {
                result.RefreshTokensRemoved,
                result.ImportJobsRemoved,
                result.AuditEventsRemoved
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        return result;
    }

    private int PurgeRefreshTokens(DateTime now)
    {
        if (_options.RefreshTokenDays <= 0)
            return 0;

        var threshold = now.AddDays(-_options.RefreshTokenDays);

        var expired = _refreshTokens.Query()
            .Where(t => (t.RevokedAtUtc != null && t.RevokedAtUtc < threshold)
                        || (t.RevokedAtUtc == null && t.ExpiresAtUtc < threshold))
            .ToList();

        foreach (var token in expired)
            _refreshTokens.Remove(token);

        return expired.Count;
    }

    private int PurgeImportJobs(DateTime now)
    {
        if (_options.ImportJobDays <= 0)
            return 0;

        var threshold = now.AddDays(-_options.ImportJobDays);

        var finished = _importJobs.Query()
            .Where(j => (j.Status == ImportJobStatus.Completed || j.Status == ImportJobStatus.Failed)
                        && j.FinishedAtUtc != null
                        && j.FinishedAtUtc < threshold)
            .ToList();

        foreach (var job in finished)
            _importJobs.Remove(job);

        return finished.Count;
    }

    private int PurgeAuditEvents(DateTime now)
    {
        if (_options.AuditEventDays <= 0)
            return 0;

        var threshold = now.AddDays(-_options.AuditEventDays);

        var purgeable = _auditEvents.Query()
            .Where(e => e.Timestamp < threshold && e.ArchivedAtUtc != null)
            .ToList();

        foreach (var evt in purgeable)
            _auditEvents.Remove(evt);

        return purgeable.Count;
    }
}
