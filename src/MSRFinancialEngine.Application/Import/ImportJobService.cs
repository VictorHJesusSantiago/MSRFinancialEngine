using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Import;

public interface IImportJobService
{
    Task<ImportJob> EnqueueAsync(Guid sourceId, string fileName, Stream content, CancellationToken ct = default);

    Task ProcessAsync(Guid jobId, CancellationToken ct = default);
}

public class ImportJobService : IImportJobService
{
    private readonly IRepository<ImportJob> _jobs;
    private readonly IRepository<Source> _sources;
    private readonly IImportService _importService;
    private readonly IImportJobSignal _signal;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImportStagingStore _staging;

    public ImportJobService(
        IRepository<ImportJob> jobs,
        IRepository<Source> sources,
        IImportService importService,
        IImportJobSignal signal,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        IImportStagingStore staging)
    {
        _jobs = jobs;
        _sources = sources;
        _importService = importService;
        _signal = signal;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _staging = staging;
    }

    public async Task<ImportJob> EnqueueAsync(Guid sourceId, string fileName, Stream content, CancellationToken ct = default)
    {
        var source = await _sources.GetByIdAsync(sourceId, ct)
            ?? throw new InvalidOperationException($"Fonte '{sourceId}' não encontrada.");

        if (!source.Active)
            throw new InvalidOperationException(
                $"A fonte '{source.Name}' está desativada e não aceita novas importações.");

        var jobId = Guid.NewGuid();
        var stagedPath = await _staging.StageAsync(jobId, content, ct);

        var job = new ImportJob
        {
            Id = jobId,
            SourceId = sourceId,
            FileName = fileName,
            StagedFilePath = stagedPath,
            Status = ImportJobStatus.Queued
        };

        await _jobs.AddAsync(job, ct);
        await _auditService.LogAsync(nameof(ImportJob), job.Id, "Queued", null, new { sourceId, fileName }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        _signal.Signal();

        return job;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, ct);

        if (job is null || job.Status != ImportJobStatus.Running)
            return;

        try
        {
            await using var content = _staging.OpenRead(job.StagedFilePath);
            var result = await _importService.ImportAsync(job.SourceId, content, ct);

            job.TotalParsed = result.TotalParsed;
            job.Imported = result.Imported;
            job.Duplicates = result.Duplicates;
            job.Status = ImportJobStatus.Completed;

            await _auditService.LogAsync(nameof(ImportJob), job.Id, "Completed", null,
                new { result.TotalParsed, result.Imported, result.Duplicates }, ct);
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;

            await _auditService.LogAsync(nameof(ImportJob), job.Id, "Failed", null, new { ex.Message }, ct);
        }
        finally
        {
            job.FinishedAtUtc = DateTime.UtcNow;
            _jobs.Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            _staging.Discard(job.StagedFilePath);
        }
    }
}

public interface IImportStagingStore
{
    Task<string> StageAsync(Guid jobId, Stream content, CancellationToken ct = default);
    Stream OpenRead(string path);
    void Discard(string path);
}
