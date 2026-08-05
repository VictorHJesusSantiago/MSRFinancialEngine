using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public static class MatchingContextFactory
{
    public static MatchingContext ForSingleCurrency(params CanonicalTransaction[] pool) =>
        new(pool, pool.ToDictionary(t => t.Id, t => (decimal?)t.Amount), "BRL");

    public static MatchingContext WithBaseAmounts(
        IReadOnlyDictionary<Guid, decimal?> baseAmounts,
        string baseCurrencyCode,
        params CanonicalTransaction[] pool) =>
        new(pool, baseAmounts, baseCurrencyCode);
}
