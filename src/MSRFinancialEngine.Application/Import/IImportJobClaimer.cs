namespace MSRFinancialEngine.Application.Import;

public interface IImportJobClaimer
{
    Task<Guid?> TryClaimNextAsync(CancellationToken ct = default);

    Task<int> ReclaimStaleAsync(TimeSpan olderThan, CancellationToken ct = default);
}

public interface IImportJobSignal
{
    void Signal();
    Task WaitAsync(TimeSpan timeout, CancellationToken ct = default);
}
