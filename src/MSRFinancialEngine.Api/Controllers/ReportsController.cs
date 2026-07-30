using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Reports;

namespace MSRFinancialEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("reconciliation-rate/{companyId:guid}")]
    public async Task<ActionResult<ReconciliationRateReport>> GetReconciliationRate(Guid companyId, CancellationToken ct) =>
        Ok(await _reportService.GetReconciliationRateAsync(companyId, ct));

    [HttpGet("divergence-aging/{companyId:guid}")]
    public async Task<ActionResult<DivergenceAgingReport>> GetDivergenceAging(Guid companyId, CancellationToken ct) =>
        Ok(await _reportService.GetDivergenceAgingAsync(companyId, ct));

    [HttpGet("user-decisions/{userId:guid}")]
    public async Task<ActionResult<List<UserDecisionHistoryEntry>>> GetUserDecisions(Guid userId, CancellationToken ct) =>
        Ok(await _reportService.GetUserDecisionHistoryAsync(userId, ct));

    [HttpGet("audit-export")]
    public async Task<ActionResult<List<AuditExportEntry>>> ExportAudit([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) =>
        Ok(await _reportService.ExportAuditTrailAsync(from, to, ct));
}
