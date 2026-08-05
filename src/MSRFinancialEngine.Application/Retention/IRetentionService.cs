namespace MSRFinancialEngine.Application.Retention;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    public int RefreshTokenDays { get; set; } = 30;

    public int ImportJobDays { get; set; } = 90;

    public int AuditEventDays { get; set; }

    public bool AutoArchiveAudit { get; set; }

    public int AuditArchiveLagDays { get; set; } = 1;
}

public class RetentionResult
{
    public int RefreshTokensRemoved { get; set; }
    public int ImportJobsRemoved { get; set; }
    public int AuditEventsRemoved { get; set; }
}

public interface IRetentionService
{
    Task<RetentionResult> PurgeAsync(CancellationToken ct = default);
}
