namespace MSRFinancialEngine.Domain.Entities;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Name { get; set; } = string.Empty;
    public SourceType Type { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RawTransaction> RawTransactions { get; set; } = new List<RawTransaction>();
}
