using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public class MatchingContext
{
    private readonly IReadOnlyDictionary<Guid, decimal?> _baseAmounts;

    public MatchingContext(
        IReadOnlyList<CanonicalTransaction> pool,
        IReadOnlyDictionary<Guid, decimal?> baseAmounts,
        string baseCurrencyCode)
    {
        Pool = pool;
        _baseAmounts = baseAmounts;
        BaseCurrencyCode = baseCurrencyCode;
    }

    public IReadOnlyList<CanonicalTransaction> Pool { get; }

    public string BaseCurrencyCode { get; }

    public decimal? BaseAmountOf(CanonicalTransaction transaction) =>
        _baseAmounts.TryGetValue(transaction.Id, out var amount) ? amount : null;

    public MatchingContext WithPool(IReadOnlyList<CanonicalTransaction> pool) =>
        new(pool, _baseAmounts, BaseCurrencyCode);
}
