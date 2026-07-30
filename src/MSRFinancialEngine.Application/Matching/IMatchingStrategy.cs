using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public class MatchAttempt
{
    public required CanonicalTransaction A { get; init; }
    public required CanonicalTransaction B { get; init; }
    public required double Score { get; init; }
}

/// <summary>
/// Estratégia de matching plugável. Cada MatchingRuleType tem uma implementação própria,
/// permitindo adicionar novas estratégias sem alterar o MatchingEngine.
/// </summary>
public interface IMatchingStrategy
{
    MatchingRuleType Type { get; }

    /// <summary>Encontra tentativas de pareamento dentro do conjunto de transações não reconciliadas.</summary>
    IEnumerable<MatchAttempt> FindCandidates(IReadOnlyList<CanonicalTransaction> pool, MatchingRule rule);
}
