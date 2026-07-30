namespace MSRFinancialEngine.Application.Reports;

public class ReconciliationRateReport
{
    public Guid CompanyId { get; set; }
    public int TotalTransactions { get; set; }
    public int Reconciled { get; set; }
    public int Pending { get; set; }
    public double AutoReconciliationRatePercent { get; set; }
    public double OverallReconciliationRatePercent { get; set; }
}

public class DivergenceAgingBucket
{
    public string Bucket { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DivergenceAgingReport
{
    public Guid CompanyId { get; set; }
    public List<DivergenceAgingBucket> Buckets { get; set; } = new();
}

public class UserDecisionHistoryEntry
{
    public Guid DivergenceId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime DecidedAt { get; set; }
    public string? Notes { get; set; }
}

public class AuditExportEntry
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string DetailsJson { get; set; } = "{}";
}
