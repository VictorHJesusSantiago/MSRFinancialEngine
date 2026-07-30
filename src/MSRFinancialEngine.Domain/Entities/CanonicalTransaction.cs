namespace MSRFinancialEngine.Domain.Entities;

/// <summary>Modelo interno único para o qual toda transação de qualquer fonte é normalizada.</summary>
public class CanonicalTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid SourceId { get; set; }
    public Guid? RawTransactionId { get; set; }
    public RawTransaction? RawTransaction { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "BRL";
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceDoc { get; set; }
    public string? AccountIdentifier { get; set; }

    /// <summary>Hash de idempotência para evitar reimportação duplicada da mesma transação.</summary>
    public string Hash { get; set; } = string.Empty;

    public bool Reconciled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MatchCandidate> MatchCandidatesAsA { get; set; } = new List<MatchCandidate>();
    public ICollection<MatchCandidate> MatchCandidatesAsB { get; set; } = new List<MatchCandidate>();
    public Divergence? Divergence { get; set; }
}
