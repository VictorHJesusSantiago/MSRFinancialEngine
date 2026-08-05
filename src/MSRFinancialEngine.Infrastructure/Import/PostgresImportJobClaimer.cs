using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;
using Npgsql;

namespace MSRFinancialEngine.Infrastructure.Import;

public class PostgresImportJobClaimer : IImportJobClaimer
{
    private static readonly SemaphoreSlim InProcessGate = new(1, 1);

    private readonly FinancialEngineDbContext _context;

    public PostgresImportJobClaimer(FinancialEngineDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> TryClaimNextAsync(CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational())
            return await TryClaimInProcessAsync(ct);

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var openedHere = false;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = """
                UPDATE import_jobs
                   SET "Status" = @running, "StartedAtUtc" = now() AT TIME ZONE 'utc'
                 WHERE "Id" = (
                       SELECT "Id" FROM import_jobs
                        WHERE "Status" = @queued
                        ORDER BY "CreatedAtUtc"
                          FOR UPDATE SKIP LOCKED
                        LIMIT 1)
                RETURNING "Id";
                """;
            command.Parameters.AddWithValue("running", (int)ImportJobStatus.Running);
            command.Parameters.AddWithValue("queued", (int)ImportJobStatus.Queued);

            var claimed = await command.ExecuteScalarAsync(ct);
            return claimed is Guid id ? id : null;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    public async Task<int> ReclaimStaleAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow - olderThan;

        var stale = await _context.ImportJobs
            .IgnoreQueryFilters()
            .Where(j => j.Status == ImportJobStatus.Running
                        && j.StartedAtUtc != null
                        && j.StartedAtUtc < threshold)
            .ToListAsync(ct);

        foreach (var job in stale)
        {
            job.Status = ImportJobStatus.Queued;
            job.StartedAtUtc = null;
        }

        if (stale.Count > 0)
            await _context.SaveChangesAsync(ct);

        return stale.Count;
    }

    private async Task<Guid?> TryClaimInProcessAsync(CancellationToken ct)
    {
        await InProcessGate.WaitAsync(ct);
        try
        {
            var job = await _context.ImportJobs
                .IgnoreQueryFilters()
                .Where(j => j.Status == ImportJobStatus.Queued)
                .OrderBy(j => j.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (job is null)
                return null;

            job.Status = ImportJobStatus.Running;
            job.StartedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return job.Id;
        }
        finally
        {
            InProcessGate.Release();
        }
    }
}

public class ImportJobSignal : IImportJobSignal
{
    private readonly SemaphoreSlim _semaphore = new(0);

    public void Signal()
    {
        if (_semaphore.CurrentCount == 0)
            _semaphore.Release();
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            await _semaphore.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
