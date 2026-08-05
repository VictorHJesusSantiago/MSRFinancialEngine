using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Infrastructure.Import;

public class ImportJobWorkerOptions
{
    public const string SectionName = "ImportWorker";

    public int PollSeconds { get; set; } = 10;

    public int StaleJobMinutes { get; set; } = 30;
}

public class ImportJobWorker : BackgroundService
{
    private readonly IImportJobSignal _signal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImportJobWorkerOptions _options;
    private readonly ILogger<ImportJobWorker> _logger;

    public ImportJobWorker(
        IImportJobSignal signal,
        IServiceScopeFactory scopeFactory,
        ImportJobWorkerOptions options,
        ILogger<ImportJobWorker> logger)
    {
        _signal = signal;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de importação iniciado.");

        await ReclaimStaleJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo do worker de importação.");
            }

            await _signal.WaitAsync(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
        }

        _logger.LogInformation("Worker de importação encerrado.");
    }

    private async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var claimer = scope.ServiceProvider.GetRequiredService<IImportJobClaimer>();
        var jobId = await claimer.TryClaimNextAsync(ct);

        if (jobId is null)
            return false;

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IImportJobService>();
            await service.ProcessAsync(jobId.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar o job de importação {JobId}", jobId);
        }

        return true;
    }

    private async Task ReclaimStaleJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var claimer = scope.ServiceProvider.GetRequiredService<IImportJobClaimer>();

            var reclaimed = await claimer.ReclaimStaleAsync(TimeSpan.FromMinutes(_options.StaleJobMinutes), ct);

            if (reclaimed > 0)
                _logger.LogWarning(
                    "{Count} job(s) de importação presos em execução foram devolvidos à fila.", reclaimed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao recuperar jobs de importação órfãos.");
        }
    }
}
