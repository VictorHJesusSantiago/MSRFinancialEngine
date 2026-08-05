using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public class DeterministicMatchingStrategyTests
{
    private static CanonicalTransaction Tx(Guid sourceId, decimal amount, DateTime date, string? reference = null, string currency = "BRL") =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Amount = amount,
            CurrencyCode = currency,
            TransactionDate = date,
            Description = "TESTE",
            ReferenceDoc = reference
        };

    [Fact]
    public void Matches_transactions_with_same_reference_and_amount_across_sources()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100m, new DateTime(2026, 1, 10), reference: "NF-001");
        var b = Tx(sourceB, 100m, new DateTime(2026, 1, 12), reference: "NF-001");

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":0}" };
        var strategy = new DeterministicMatchingStrategy();

        var attempts = strategy.FindCandidates(MatchingContextFactory.ForSingleCurrency(a, b), rule).ToList();

        Assert.Single(attempts);
        Assert.Equal(1.0, attempts[0].Score);
    }

    [Fact]
    public void Does_not_match_transactions_from_the_same_source()
    {
        var sourceA = Guid.NewGuid();
        var a = Tx(sourceA, 100m, new DateTime(2026, 1, 10), reference: "NF-001");
        var b = Tx(sourceA, 100m, new DateTime(2026, 1, 10), reference: "NF-001");

        var rule = new MatchingRule { ConfigJson = "{}" };
        var strategy = new DeterministicMatchingStrategy();

        var attempts = strategy.FindCandidates(MatchingContextFactory.ForSingleCurrency(a, b), rule).ToList();

        Assert.Empty(attempts);
    }

    [Fact]
    public void Does_not_match_when_amount_outside_tolerance()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100m, new DateTime(2026, 1, 10), reference: "NF-001");
        var b = Tx(sourceB, 150m, new DateTime(2026, 1, 10), reference: "NF-001");

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":0}" };
        var strategy = new DeterministicMatchingStrategy();

        var attempts = strategy.FindCandidates(MatchingContextFactory.ForSingleCurrency(a, b), rule).ToList();

        Assert.Empty(attempts);
    }

    [Fact]
    public void Matches_by_amount_and_date_tolerance_when_no_reference()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100m, new DateTime(2026, 1, 10));
        var b = Tx(sourceB, 100m, new DateTime(2026, 1, 11));

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2}" };
        var strategy = new DeterministicMatchingStrategy();

        var attempts = strategy.FindCandidates(MatchingContextFactory.ForSingleCurrency(a, b), rule).ToList();

        Assert.Single(attempts);
        Assert.Equal(0.95, attempts[0].Score);
    }
}
