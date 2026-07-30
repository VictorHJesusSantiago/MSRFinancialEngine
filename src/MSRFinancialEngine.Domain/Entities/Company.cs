namespace MSRFinancialEngine.Domain.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = "BRL";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<CanonicalTransaction> Transactions { get; set; } = new List<CanonicalTransaction>();
    public ICollection<MatchingRule> MatchingRules { get; set; } = new List<MatchingRule>();
}
