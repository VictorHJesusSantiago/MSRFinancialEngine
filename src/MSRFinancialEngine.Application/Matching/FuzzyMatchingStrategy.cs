using System.Text.Json;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

/// <summary>
/// Para o que sobra após o matching determinístico: combina similaridade textual da
/// descrição com tolerância de valor/data, gerando um score de confiança de 0.0 a 1.0.
/// </summary>
public class FuzzyMatchingStrategy : IMatchingStrategy
{
    public MatchingRuleType Type => MatchingRuleType.Fuzzy;

    public IEnumerable<MatchAttempt> FindCandidates(IReadOnlyList<CanonicalTransaction> pool, MatchingRule rule)
    {
        var config = JsonSerializer.Deserialize<FuzzyRuleConfig>(rule.ConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FuzzyRuleConfig();

        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = i + 1; j < pool.Count; j++)
            {
                var a = pool[i];
                var b = pool[j];

                if (a.SourceId == b.SourceId)
                    continue;

                if (!string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                var amountDiff = Math.Abs(a.Amount - b.Amount);
                var daysDiff = Math.Abs((a.TransactionDate.Date - b.TransactionDate.Date).TotalDays);

                if (amountDiff > config.ToleranceAmount || daysDiff > config.ToleranceDays)
                    continue;

                var textSimilarity = StringSimilarity.NormalizedSimilarity(a.Description, b.Description);

                // Score combina similaridade textual (peso maior) com proximidade de valor/data.
                var amountScore = config.ToleranceAmount == 0
                    ? 1.0
                    : 1.0 - (double)(amountDiff / config.ToleranceAmount) * 0.5;
                var dateScore = config.ToleranceDays == 0
                    ? 1.0
                    : 1.0 - (daysDiff / config.ToleranceDays) * 0.5;

                var score = (textSimilarity * 0.6) + (amountScore * 0.25) + (dateScore * 0.15);
                score = Math.Clamp(score, 0.0, 1.0);

                if (score >= config.MinScore)
                    yield return new MatchAttempt { A = a, B = b, Score = score };
            }
        }
    }
}

public class FuzzyRuleConfig
{
    public double MinScore { get; set; } = 0.75;
    public decimal ToleranceAmount { get; set; } = 0.05m;
    public int ToleranceDays { get; set; } = 3;
}
