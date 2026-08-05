using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Retention;

namespace MSRFinancialEngine.Infrastructure.Retention;

public class RetentionWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionWorker> _logger;

    public RetentionWorker(IServiceScopeFactory scopeFactory, ILogger<RetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<RetentionOptions>();

            if (options.AutoArchiveAudit)
            {
                var archive = await scope.ServiceProvider
                    .GetRequiredService<IAuditArchiveService>()
                    .ArchivePendingAsync(options.AuditArchiveLagDays, ct);

                if (archive is not null)
                    _logger.LogInformation(
                        "Auditoria arquivada: {Count} eventos de {From:o} a {To:o} em {Location}",
                        archive.EventCount, archive.FromUtc, archive.ToUtc, archive.Location);
            }

            var service = scope.ServiceProvider.GetRequiredService<IRetentionService>();
            var result = await service.PurgeAsync(ct);

            if (result.RefreshTokensRemoved + result.ImportJobsRemoved + result.AuditEventsRemoved > 0)
                _logger.LogInformation(
                    "Expurgo concluído: {Tokens} refresh tokens, {Jobs} jobs, {Events} eventos de auditoria.",
                    result.RefreshTokensRemoved, result.ImportJobsRemoved, result.AuditEventsRemoved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar o expurgo de retenção.");
        }
    }
}
