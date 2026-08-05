using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record ImportJobResponse(
    Guid Id,
    Guid SourceId,
    string FileName,
    string Status,
    int TotalParsed,
    int Imported,
    int Duplicates,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly IReprocessService _reprocessService;
    private readonly IImportJobService _importJobService;
    private readonly IRepository<ImportJob> _jobs;

    public ImportController(
        IImportService importService,
        IReprocessService reprocessService,
        IImportJobService importJobService,
        IRepository<ImportJob> jobs)
    {
        _importService = importService;
        _reprocessService = reprocessService;
        _importJobService = importJobService;
        _jobs = jobs;
    }

    [HttpPost("{sourceId:guid}")]
    public async Task<ActionResult<ImportResult>> Import(Guid sourceId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Arquivo não informado ou vazio.");

        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(sourceId, stream, ct);
        return Ok(result);
    }

    [HttpPost("{sourceId:guid}/async")]
    public async Task<ActionResult<ImportJobResponse>> ImportAsync(Guid sourceId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Arquivo não informado ou vazio.");

        await using var stream = file.OpenReadStream();
        var job = await _importJobService.EnqueueAsync(sourceId, file.FileName, stream, ct);

        return AcceptedAtAction(nameof(GetJob), new { jobId = job.Id }, ToResponse(job));
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<ImportJobResponse>> GetJob(Guid jobId, CancellationToken ct)
    {
        var job = await _jobs.GetByIdAsync(jobId, ct);
        return job is null ? NotFound() : Ok(ToResponse(job));
    }

    [HttpGet("jobs")]
    public ActionResult<PagedResult<ImportJobResponse>> GetJobs(
        [FromQuery] Guid? sourceId, [FromQuery] ImportJobStatus? status, [FromQuery] PageRequest pagination)
    {
        var query = _jobs.Query();

        if (sourceId.HasValue)
            query = query.Where(j => j.SourceId == sourceId.Value);
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        return Ok(query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Select(j => ToResponse(j))
            .ToPagedResult(pagination));
    }

    [HttpPost("{sourceId:guid}/invalidate")]
    public async Task<ActionResult<ReprocessResult>> Invalidate(Guid sourceId, CancellationToken ct) =>
        Ok(await _reprocessService.InvalidateSourceAsync(sourceId, ct));

    private static ImportJobResponse ToResponse(ImportJob j) => new(
        j.Id, j.SourceId, j.FileName, j.Status.ToString(),
        j.TotalParsed, j.Imported, j.Duplicates, j.ErrorMessage,
        j.CreatedAtUtc, j.StartedAtUtc, j.FinishedAtUtc);
}
