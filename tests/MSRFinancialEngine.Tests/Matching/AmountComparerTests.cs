using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public class AmountComparerTests
{
    [Fact]
    public void Same_sign_mode_compares_values_directly()
    {
        Assert.Equal(0m, AmountComparer.Difference(100m, 100m, matchOppositeSigns: false));
        Assert.Equal(200m, AmountComparer.Difference(100m, -100m, matchOppositeSigns: false));
    }

    [Fact]
    public void Opposite_sign_mode_matches_debit_against_credit_of_same_magnitude()
    {
        Assert.Equal(0m, AmountComparer.Difference(1250.75m, -1250.75m, matchOppositeSigns: true));
        Assert.Equal(0.05m, AmountComparer.Difference(100.00m, -100.05m, matchOppositeSigns: true));
    }

    [Fact]
    public void Opposite_sign_mode_rejects_pairs_with_the_same_sign()
    {
        Assert.Null(AmountComparer.Difference(100m, 100m, matchOppositeSigns: true));
        Assert.Null(AmountComparer.Difference(-100m, -100m, matchOppositeSigns: true));
    }

    [Fact]
    public void Opposite_sign_mode_rejects_zero_amounts()
    {
        Assert.Null(AmountComparer.Difference(0m, -100m, matchOppositeSigns: true));
    }
}

public class OppositeSignMatchingTests
{
    private static CanonicalTransaction Tx(Guid sourceId, decimal amount, string reference, string description = "TESTE") => new()
    {
        Id = Guid.NewGuid(),
        SourceId = sourceId,
        Amount = amount,
        CurrencyCode = "BRL",
        TransactionDate = new DateTime(2026, 1, 10),
        Description = description,
        ReferenceDoc = reference
    };

    [Fact]
    public void Deterministic_rule_matches_invoice_against_bank_payment()
    {
        var invoice = Tx(Guid.NewGuid(), 1250.75m, "15");
        var payment = Tx(Guid.NewGuid(), -1250.75m, "15");

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"matchOppositeSigns\":true}" };

        var attempts = new DeterministicMatchingStrategy().FindCandidates(MatchingContextFactory.ForSingleCurrency(invoice, payment), rule).ToList();

        Assert.Single(attempts);
        Assert.Equal(1.0, attempts[0].Score);
    }

    [Fact]
    public void Default_rule_still_requires_same_sign()
    {
        var invoice = Tx(Guid.NewGuid(), 1250.75m, "15");
        var payment = Tx(Guid.NewGuid(), -1250.75m, "15");

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2}" };

        var attempts = new DeterministicMatchingStrategy().FindCandidates(MatchingContextFactory.ForSingleCurrency(invoice, payment), rule).ToList();

        Assert.Empty(attempts);
    }

    [Fact]
    public void Fuzzy_rule_matches_opposite_signs_when_enabled()
    {
        var invoice = Tx(Guid.NewGuid(), 1250.75m, "15", "NF-E 15 FORNECEDOR ACME LTDA");
        var payment = Tx(Guid.NewGuid(), -1250.75m, "OUTRA", "PAGAMENTO FORNECEDOR ACME LTDA");

        var rule = new MatchingRule
        {
            ConfigJson = "{\"minScore\":0.5,\"toleranceAmount\":0.05,\"toleranceDays\":3,\"matchOppositeSigns\":true}"
        };

        var attempts = new FuzzyMatchingStrategy().FindCandidates(MatchingContextFactory.ForSingleCurrency(invoice, payment), rule).ToList();

        Assert.Single(attempts);
        Assert.True(attempts[0].Score >= 0.5);
    }
}
