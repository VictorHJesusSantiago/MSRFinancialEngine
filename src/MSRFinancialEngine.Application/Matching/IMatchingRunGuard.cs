namespace MSRFinancialEngine.Application.Matching;

public interface IMatchingRunGuard
{
    Task<IAsyncDisposable?> TryAcquireAsync(Guid companyId, CancellationToken ct = default);
}

public class MatchingAlreadyRunningException : InvalidOperationException
{
    public MatchingAlreadyRunningException(Guid companyId)
        : base($"Já existe uma execução de matching em andamento para a empresa '{companyId}'. Aguarde a conclusão.")
    {
    }
}
