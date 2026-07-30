using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public class FuzzyMatchingStrategyTests
{
    private static CanonicalTransaction Tx(Guid sourceId, decimal amount, DateTime date, string description) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Amount = amount,
            CurrencyCode = "BRL",
            TransactionDate = date,
            Description = description
        };

    [Fact]
    public void Matches_similar_descriptions_within_tolerance()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100.00m, new DateTime(2026, 1, 10), "PAGAMENTO FORNECEDOR ACME LTDA");
        var b = Tx(sourceB, 100.02m, new DateTime(2026, 1, 11), "PAGTO FORNECEDOR ACME LTDA");

        var rule = new MatchingRule { ConfigJson = "{\"minScore\":0.6,\"toleranceAmount\":0.05,\"toleranceDays\":3}" };
        var strategy = new FuzzyMatchingStrategy();

        var attempts = strategy.FindCandidates(new[] { a, b }, rule).ToList();

        Assert.Single(attempts);
        Assert.True(attempts[0].Score >= 0.6);
    }

    [Fact]
    public void Does_not_match_when_below_min_score()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100.00m, new DateTime(2026, 1, 10), "PAGAMENTO FORNECEDOR ACME LTDA");
        var b = Tx(sourceB, 100.02m, new DateTime(2026, 1, 11), "RECEBIMENTO CLIENTE ZETA SA");

        var rule = new MatchingRule { ConfigJson = "{\"minScore\":0.9,\"toleranceAmount\":0.05,\"toleranceDays\":3}" };
        var strategy = new FuzzyMatchingStrategy();

        var attempts = strategy.FindCandidates(new[] { a, b }, rule).ToList();

        Assert.Empty(attempts);
    }

    [Fact]
    public void Does_not_match_when_outside_date_tolerance()
    {
        var sourceA = Guid.NewGuid();
        var sourceB = Guid.NewGuid();
        var a = Tx(sourceA, 100.00m, new DateTime(2026, 1, 1), "PAGAMENTO FORNECEDOR ACME LTDA");
        var b = Tx(sourceB, 100.00m, new DateTime(2026, 2, 1), "PAGAMENTO FORNECEDOR ACME LTDA");

        var rule = new MatchingRule { ConfigJson = "{\"minScore\":0.5,\"toleranceAmount\":0.05,\"toleranceDays\":3}" };
        var strategy = new FuzzyMatchingStrategy();

        var attempts = strategy.FindCandidates(new[] { a, b }, rule).ToList();

        Assert.Empty(attempts);
    }
}
