namespace MSRFinancialEngine.Domain.Entities;

public class MatchingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Name { get; set; } = string.Empty;
    public MatchingRuleType Type { get; set; }

    /// <summary>
    /// Configuração da regra. Para Deterministic: {"toleranceAmount":0.0,"toleranceDays":0}.
    /// Para Fuzzy: {"minScore":0.75,"toleranceAmount":0.05,"toleranceDays":3}.
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public int Priority { get; set; }
    public bool Active { get; set; } = true;

    public ICollection<MatchCandidate> MatchCandidates { get; set; } = new List<MatchCandidate>();
}
