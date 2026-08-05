using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public class DivergenceReasonAnalyzerTests
{
    private static CanonicalTransaction Tx(Guid sourceId, decimal amount, DateTime date, string currency = "BRL") => new()
    {
        Id = Guid.NewGuid(),
        SourceId = sourceId,
        Amount = amount,
        CurrencyCode = currency,
        TransactionDate = date,
        Description = "TESTE"
    };

    [Fact]
    public void Alone_in_the_pool_is_no_candidate()
    {
        var solo = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10));
        var context = MatchingContextFactory.ForSingleCurrency(solo);

        var reason = DivergenceReasonAnalyzer.Analyze(solo, context, Array.Empty<MatchAttempt>());

        Assert.Equal(DivergenceReason.NoCandidate, reason);
    }

    [Fact]
    public void Same_amount_on_a_distant_date_is_date_out_of_tolerance()
    {
        var a = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 1));
        var b = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 3, 1));
        var context = MatchingContextFactory.ForSingleCurrency(a, b);

        var reason = DivergenceReasonAnalyzer.Analyze(a, context, Array.Empty<MatchAttempt>());

        Assert.Equal(DivergenceReason.DateOutOfTolerance, reason);
    }

    [Fact]
    public void Same_date_with_different_amount_is_amount_out_of_tolerance()
    {
        var a = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10));
        var b = Tx(Guid.NewGuid(), 175m, new DateTime(2026, 1, 10));
        var context = MatchingContextFactory.ForSingleCurrency(a, b);

        var reason = DivergenceReasonAnalyzer.Analyze(a, context, Array.Empty<MatchAttempt>());

        Assert.Equal(DivergenceReason.AmountOutOfTolerance, reason);
    }

    [Fact]
    public void More_than_one_pending_candidate_is_ambiguity()
    {
        var target = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10));
        var first = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10));
        var second = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10));
        var context = MatchingContextFactory.ForSingleCurrency(target, first, second);

        var attempts = new[]
        {
            new MatchAttempt { A = target, B = first, Score = 0.8 },
            new MatchAttempt { A = target, B = second, Score = 0.79 }
        };

        var reason = DivergenceReasonAnalyzer.Analyze(target, context, attempts);

        Assert.Equal(DivergenceReason.MultipleCandidates, reason);
    }

    [Fact]
    public void Foreign_currency_without_rate_is_currency_mismatch()
    {
        var usd = Tx(Guid.NewGuid(), 100m, new DateTime(2026, 1, 10), "USD");
        var brl = Tx(Guid.NewGuid(), 500m, new DateTime(2026, 1, 10));

        var context = MatchingContextFactory.WithBaseAmounts(
            new Dictionary<Guid, decimal?> { [usd.Id] = null, [brl.Id] = 500m }, "BRL", usd, brl);

        var reason = DivergenceReasonAnalyzer.Analyze(usd, context, Array.Empty<MatchAttempt>());

        Assert.Equal(DivergenceReason.CurrencyMismatch, reason);
    }

    [Fact]
    public void Transactions_from_the_same_source_are_not_counterparts()
    {
        var sameSource = Guid.NewGuid();
        var a = Tx(sameSource, 100m, new DateTime(2026, 1, 1));
        var b = Tx(sameSource, 100m, new DateTime(2026, 3, 1));
        var context = MatchingContextFactory.ForSingleCurrency(a, b);

        var reason = DivergenceReasonAnalyzer.Analyze(a, context, Array.Empty<MatchAttempt>());

        Assert.Equal(DivergenceReason.NoCandidate, reason);
    }
}
