using System.Text.Json;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public class DeterministicMatchingStrategy : IMatchingStrategy
{
    public MatchingRuleType Type => MatchingRuleType.Deterministic;

    public IEnumerable<MatchAttempt> FindCandidates(MatchingContext context, MatchingRule rule)
    {
        var config = JsonSerializer.Deserialize<DeterministicRuleConfig>(rule.ConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DeterministicRuleConfig();

        var pool = context.Pool;

        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = i + 1; j < pool.Count; j++)
            {
                var a = pool[i];
                var b = pool[j];

                if (a.SourceId == b.SourceId)
                    continue;

                var amountDiff = AmountComparer.Difference(
                    context, a, b, config.MatchOppositeSigns, config.CrossCurrency);
                if (amountDiff is null)
                    continue;

                var sameReference = !string.IsNullOrWhiteSpace(a.ReferenceDoc)
                    && string.Equals(a.ReferenceDoc, b.ReferenceDoc, StringComparison.OrdinalIgnoreCase);

                var daysDiff = Math.Abs((a.TransactionDate.Date - b.TransactionDate.Date).TotalDays);

                var withinAmountTolerance = amountDiff.Value <= config.ToleranceAmount;
                var withinDateTolerance = daysDiff <= config.ToleranceDays;

                if (sameReference && withinAmountTolerance)
                {
                    yield return new MatchAttempt { A = a, B = b, Score = 1.0 };
                }
                else if (withinAmountTolerance && withinDateTolerance)
                {
                    yield return new MatchAttempt { A = a, B = b, Score = 0.95 };
                }
            }
        }
    }
}

public class DeterministicRuleConfig
{
    public decimal ToleranceAmount { get; set; } = 0m;
    public int ToleranceDays { get; set; } = 0;

    public bool MatchOppositeSigns { get; set; }

    public bool CrossCurrency { get; set; }
}
