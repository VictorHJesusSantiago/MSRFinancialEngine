using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Matching;

public class CrossCurrencyMatchingTests
{
    private static CanonicalTransaction Tx(Guid sourceId, decimal amount, string currency, string reference) => new()
    {
        Id = Guid.NewGuid(),
        SourceId = sourceId,
        Amount = amount,
        CurrencyCode = currency,
        TransactionDate = new DateTime(2026, 1, 10),
        Description = "PAGAMENTO",
        ReferenceDoc = reference
    };

    [Fact]
    public void Different_currencies_do_not_match_when_cross_currency_is_disabled()
    {
        var usd = Tx(Guid.NewGuid(), 100m, "USD", "NF-1");
        var brl = Tx(Guid.NewGuid(), 500m, "BRL", "NF-1");

        var context = MatchingContextFactory.WithBaseAmounts(
            new Dictionary<Guid, decimal?> { [usd.Id] = 500m, [brl.Id] = 500m }, "BRL", usd, brl);

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2}" };

        Assert.Empty(new DeterministicMatchingStrategy().FindCandidates(context, rule));
    }

    [Fact]
    public void Different_currencies_match_in_base_currency_when_enabled()
    {
        var usd = Tx(Guid.NewGuid(), 100m, "USD", "NF-1");
        var brl = Tx(Guid.NewGuid(), 500m, "BRL", "NF-1");

        var context = MatchingContextFactory.WithBaseAmounts(
            new Dictionary<Guid, decimal?> { [usd.Id] = 500m, [brl.Id] = 500m }, "BRL", usd, brl);

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"crossCurrency\":true}" };

        var attempts = new DeterministicMatchingStrategy().FindCandidates(context, rule).ToList();

        Assert.Single(attempts);
        Assert.Equal(1.0, attempts[0].Score);
    }

    [Fact]
    public void Cross_currency_pair_without_exchange_rate_is_not_matched()
    {
        var usd = Tx(Guid.NewGuid(), 100m, "USD", "NF-1");
        var brl = Tx(Guid.NewGuid(), 500m, "BRL", "NF-1");

        var context = MatchingContextFactory.WithBaseAmounts(
            new Dictionary<Guid, decimal?> { [usd.Id] = null, [brl.Id] = 500m }, "BRL", usd, brl);

        var rule = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"crossCurrency\":true}" };

        Assert.Empty(new DeterministicMatchingStrategy().FindCandidates(context, rule));
    }

    [Fact]
    public void Cross_currency_tolerance_applies_in_base_currency()
    {
        var usd = Tx(Guid.NewGuid(), 100m, "USD", "NF-1");
        var brl = Tx(Guid.NewGuid(), 500.03m, "BRL", "NF-1");

        var context = MatchingContextFactory.WithBaseAmounts(
            new Dictionary<Guid, decimal?> { [usd.Id] = 500m, [brl.Id] = 500.03m }, "BRL", usd, brl);

        var strict = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"crossCurrency\":true}" };
        var tolerant = new MatchingRule { ConfigJson = "{\"toleranceAmount\":0.05,\"toleranceDays\":2,\"crossCurrency\":true}" };

        Assert.Empty(new DeterministicMatchingStrategy().FindCandidates(context, strict));
        Assert.Single(new DeterministicMatchingStrategy().FindCandidates(context, tolerant));
    }
}

public class MatchingEngineCurrencyTests
{
    [Fact]
    public async Task Engine_converts_using_historical_rate_and_reconciles_across_currencies()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Multimoeda", BaseCurrencyCode = "BRL" };
        var bank = new Source { CompanyId = company.Id, Name = "Banco BRL", Type = SourceType.BankStatementCsv };
        var erp = new Source { CompanyId = company.Id, Name = "ERP USD", Type = SourceType.ErpJson };
        context.Companies.Add(company);
        context.Sources.AddRange(bank, erp);

        context.ExchangeRates.Add(new ExchangeRate
        {
            CurrencyCode = "USD", BaseCurrencyCode = "BRL",
            Date = new DateOnly(2026, 1, 1), RateToBase = 5.00m
        });

        context.MatchingRules.Add(new MatchingRule
        {
            CompanyId = company.Id,
            Name = "Cross currency",
            Type = MatchingRuleType.Deterministic,
            ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"crossCurrency\":true}",
            Priority = 1
        });

        var brlTx = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = bank.Id, Amount = 500m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-1", Hash = "h1"
        };
        var usdTx = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = erp.Id, Amount = 100m, CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-1", Hash = "h2"
        };
        context.CanonicalTransactions.AddRange(brlTx, usdTx);
        await context.SaveChangesAsync();

        var result = await MatchingEngineFactory.Build(context).RunForCompanyAsync(company.Id);

        Assert.Equal(1, result.AutoApproved);
        Assert.Equal(0, result.MissingExchangeRates);

        await context.Entry(brlTx).ReloadAsync();
        await context.Entry(usdTx).ReloadAsync();
        Assert.True(brlTx.Reconciled);
        Assert.True(usdTx.Reconciled);
    }

    [Fact]
    public async Task Missing_exchange_rate_produces_currency_mismatch_divergence()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Sem taxa", BaseCurrencyCode = "BRL" };
        var bank = new Source { CompanyId = company.Id, Name = "Banco", Type = SourceType.BankStatementCsv };
        var erp = new Source { CompanyId = company.Id, Name = "ERP", Type = SourceType.ErpJson };
        context.Companies.Add(company);
        context.Sources.AddRange(bank, erp);

        context.MatchingRules.Add(new MatchingRule
        {
            CompanyId = company.Id,
            Name = "Cross currency",
            Type = MatchingRuleType.Deterministic,
            ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":2,\"crossCurrency\":true}",
            Priority = 1
        });

        var brlTx = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = bank.Id, Amount = 500m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-1", Hash = "h1"
        };
        var usdTx = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = erp.Id, Amount = 100m, CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-1", Hash = "h2"
        };
        context.CanonicalTransactions.AddRange(brlTx, usdTx);
        await context.SaveChangesAsync();

        var result = await MatchingEngineFactory.Build(context).RunForCompanyAsync(company.Id);

        Assert.Equal(0, result.AutoApproved);
        Assert.Equal(1, result.MissingExchangeRates);

        var usdDivergence = context.Divergences.Single(d => d.TransactionId == usdTx.Id);
        Assert.Equal(DivergenceReason.CurrencyMismatch, usdDivergence.Reason);
    }
}
