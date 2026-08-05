using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Application.Reports;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record ArchiveAuditRequest([Required] DateTime FromUtc, [Required] DateTime ToUtc);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IAuditArchiveService _archiveService;
    private readonly IRepository<AuditArchive> _archives;
    private readonly ICurrentUser _currentUser;

    public ReportsController(
        IReportService reportService,
        IAuditArchiveService archiveService,
        IRepository<AuditArchive> archives,
        ICurrentUser currentUser)
    {
        _reportService = reportService;
        _archiveService = archiveService;
        _archives = archives;
        _currentUser = currentUser;
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

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("audit-archive")]
    public async Task<ActionResult<AuditArchive>> ArchiveAudit(ArchiveAuditRequest request, CancellationToken ct) =>
        Ok(await _archiveService.ArchiveAsync(request.FromUtc, request.ToUtc, _currentUser.UserId, ct));

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet("audit-archive")]
    public ActionResult<PagedResult<AuditArchive>> GetArchives([FromQuery] PageRequest pagination) =>
        Ok(_archives.Query().OrderByDescending(a => a.CreatedAtUtc).ToPagedResult(pagination));

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet("audit-archive/{id:guid}/verify")]
    public async Task<ActionResult<object>> VerifyArchive(Guid id, CancellationToken ct) =>
        Ok(new { ArchiveId = id, Intact = await _archiveService.VerifyAsync(id, ct) });
}
