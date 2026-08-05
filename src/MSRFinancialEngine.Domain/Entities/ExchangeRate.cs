namespace MSRFinancialEngine.Domain.Entities;

public class ExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CurrencyCode { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal RateToBase { get; set; }
}
