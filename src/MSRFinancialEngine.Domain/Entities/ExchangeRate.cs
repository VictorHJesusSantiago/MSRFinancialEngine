namespace MSRFinancialEngine.Domain.Entities;

/// <summary>
/// Taxa de câmbio histórica por data. Armazenada (não recalculada) para permitir
/// reconciliação auditável — a taxa usada em uma reconciliação deve ser a vigente
/// na data da transação, não a taxa atual.
/// </summary>
public class ExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CurrencyCode { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal RateToBase { get; set; }
}
