namespace MSRFinancialEngine.Application.Currency;

public interface ICurrencyConversionService
{
    /// <summary>
    /// Converte um valor de uma moeda para a moeda base usando a taxa histórica vigente
    /// na data informada (não recalcula com taxa atual, para manter reconciliação auditável).
    /// </summary>
    Task<decimal> ConvertToBaseAsync(decimal amount, string currencyCode, string baseCurrencyCode, DateOnly date, CancellationToken ct = default);
}
