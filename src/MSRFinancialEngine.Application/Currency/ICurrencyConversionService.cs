namespace MSRFinancialEngine.Application.Currency;

public interface ICurrencyConversionService
{
    Task<decimal> ConvertToBaseAsync(decimal amount, string currencyCode, string baseCurrencyCode, DateOnly date, CancellationToken ct = default);
}
