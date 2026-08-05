using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Currency;

public class CurrencyConversionServiceTests
{
    [Fact]
    public async Task Same_currency_returns_amount_unchanged()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new CurrencyConversionService(new EfRepository<ExchangeRate>(context));

        var result = await service.ConvertToBaseAsync(100m, "BRL", "BRL", new DateOnly(2026, 1, 10));

        Assert.Equal(100m, result);
    }

    [Fact]
    public async Task Uses_historical_rate_in_effect_on_the_transaction_date()
    {
        await using var context = TestDbContextFactory.Create();

        context.ExchangeRates.AddRange(
            new ExchangeRate { CurrencyCode = "USD", BaseCurrencyCode = "BRL", Date = new DateOnly(2026, 1, 1), RateToBase = 5.00m },
            new ExchangeRate { CurrencyCode = "USD", BaseCurrencyCode = "BRL", Date = new DateOnly(2026, 1, 15), RateToBase = 6.00m });
        await context.SaveChangesAsync();

        var service = new CurrencyConversionService(new EfRepository<ExchangeRate>(context));

        var result = await service.ConvertToBaseAsync(10m, "USD", "BRL", new DateOnly(2026, 1, 10));

        Assert.Equal(50.00m, result);
    }

    [Fact]
    public async Task Uses_the_exact_rate_when_one_exists_for_that_day()
    {
        await using var context = TestDbContextFactory.Create();

        context.ExchangeRates.AddRange(
            new ExchangeRate { CurrencyCode = "USD", BaseCurrencyCode = "BRL", Date = new DateOnly(2026, 1, 1), RateToBase = 5.00m },
            new ExchangeRate { CurrencyCode = "USD", BaseCurrencyCode = "BRL", Date = new DateOnly(2026, 1, 15), RateToBase = 6.00m });
        await context.SaveChangesAsync();

        var service = new CurrencyConversionService(new EfRepository<ExchangeRate>(context));

        var result = await service.ConvertToBaseAsync(10m, "USD", "BRL", new DateOnly(2026, 1, 15));

        Assert.Equal(60.00m, result);
    }

    [Fact]
    public async Task Throws_when_no_rate_exists_for_the_date_or_earlier()
    {
        await using var context = TestDbContextFactory.Create();

        context.ExchangeRates.Add(
            new ExchangeRate { CurrencyCode = "USD", BaseCurrencyCode = "BRL", Date = new DateOnly(2026, 6, 1), RateToBase = 5.00m });
        await context.SaveChangesAsync();

        var service = new CurrencyConversionService(new EfRepository<ExchangeRate>(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConvertToBaseAsync(10m, "USD", "BRL", new DateOnly(2026, 1, 10)));
    }
}
