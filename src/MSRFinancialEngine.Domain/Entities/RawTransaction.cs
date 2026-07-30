namespace MSRFinancialEngine.Domain.Entities;

/// <summary>Payload bruto exatamente como veio da fonte, antes de qualquer normalização.</summary>
public class RawTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public Source? Source { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public bool Normalized { get; set; }

    public CanonicalTransaction? CanonicalTransaction { get; set; }
}
