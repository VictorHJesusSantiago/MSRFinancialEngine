namespace MSRFinancialEngine.Application.Reports;

public interface IReportService
{
    Task<ReconciliationRateReport> GetReconciliationRateAsync(Guid companyId, CancellationToken ct = default);
    Task<DivergenceAgingReport> GetDivergenceAgingAsync(Guid companyId, CancellationToken ct = default);
    Task<List<UserDecisionHistoryEntry>> GetUserDecisionHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<List<AuditExportEntry>> ExportAuditTrailAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
