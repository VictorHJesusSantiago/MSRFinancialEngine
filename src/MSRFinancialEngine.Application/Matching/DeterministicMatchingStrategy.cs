using System.Text.Json;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

/// <summary>
/// Casa transações por chave exata: mesmo valor (dentro de tolerância opcional) e mesma
/// data (dentro de tolerância opcional), priorizando ReferenceDoc idêntico quando presente.
/// Regras determinísticas rodam primeiro por serem baratas e não ambíguas.
/// </summary>
public class DeterministicMatchingStrategy : IMatchingStrategy
{
    public MatchingRuleType Type => MatchingRuleType.Deterministic;

    public IEnumerable<MatchAttempt> FindCandidates(IReadOnlyList<CanonicalTransaction> pool, MatchingRule rule)
    {
        var config = JsonSerializer.Deserialize<DeterministicRuleConfig>(rule.ConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DeterministicRuleConfig();

        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = i + 1; j < pool.Count; j++)
            {
                var a = pool[i];
                var b = pool[j];

                if (a.SourceId == b.SourceId)
                    continue; // matching é entre fontes diferentes (ex: extrato x ERP)

                if (!string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                var sameReference = !string.IsNullOrWhiteSpace(a.ReferenceDoc)
                    && string.Equals(a.ReferenceDoc, b.ReferenceDoc, StringComparison.OrdinalIgnoreCase);

                var amountDiff = Math.Abs(a.Amount - b.Amount);
                var daysDiff = Math.Abs((a.TransactionDate.Date - b.TransactionDate.Date).TotalDays);

                var withinAmountTolerance = amountDiff <= config.ToleranceAmount;
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
}
