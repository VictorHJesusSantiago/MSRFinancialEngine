using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Currency;

public class CurrencyConversionService : ICurrencyConversionService
{
    private readonly IRepository<ExchangeRate> _exchangeRateRepository;

    public CurrencyConversionService(IRepository<ExchangeRate> exchangeRateRepository)
    {
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<decimal> ConvertToBaseAsync(decimal amount, string currencyCode, string baseCurrencyCode, DateOnly date, CancellationToken ct = default)
    {
        if (string.Equals(currencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            return amount;

        // Taxa vigente na data: a mais recente com Date <= data solicitada.
        var rate = await Task.FromResult(_exchangeRateRepository.Query()
            .Where(r => r.CurrencyCode == currencyCode && r.BaseCurrencyCode == baseCurrencyCode && r.Date <= date)
            .OrderByDescending(r => r.Date)
            .FirstOrDefault());

        if (rate is null)
            throw new InvalidOperationException(
                $"Nenhuma taxa de câmbio encontrada para converter {currencyCode} -> {baseCurrencyCode} na data {date:yyyy-MM-dd} ou anterior.");

        return Math.Round(amount * rate.RateToBase, 2, MidpointRounding.AwayFromZero);
    }
}
