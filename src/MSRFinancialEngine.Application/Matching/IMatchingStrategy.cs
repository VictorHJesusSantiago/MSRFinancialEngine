using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public class MatchAttempt
{
    public required CanonicalTransaction A { get; init; }
    public required CanonicalTransaction B { get; init; }
    public required double Score { get; init; }
}

public interface IMatchingStrategy
{
    MatchingRuleType Type { get; }

    IEnumerable<MatchAttempt> FindCandidates(MatchingContext context, MatchingRule rule);
}
