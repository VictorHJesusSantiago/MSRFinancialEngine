using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public static class DivergenceReasonAnalyzer
{
    public static DivergenceReason Analyze(
        CanonicalTransaction transaction,
        MatchingContext context,
        IReadOnlyList<MatchAttempt> pendingCandidates)
    {
        if (pendingCandidates.Count > 1)
            return DivergenceReason.MultipleCandidates;

        var counterparts = context.Pool
            .Where(t => t.Id != transaction.Id && t.SourceId != transaction.SourceId)
            .ToList();

        var isForeignCurrency = !string.Equals(transaction.CurrencyCode, context.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase);
        if (isForeignCurrency && context.BaseAmountOf(transaction) is null
            && counterparts.Any(c => !string.Equals(c.CurrencyCode, transaction.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            return DivergenceReason.CurrencyMismatch;
        }

        if (counterparts.Count == 0)
            return DivergenceReason.NoCandidate;

        var sameAmountDifferentDate = counterparts.Any(c =>
            SameCurrency(transaction, c)
            && Math.Abs(c.Amount - transaction.Amount) == 0m
            && c.TransactionDate.Date != transaction.TransactionDate.Date);

        if (sameAmountDifferentDate)
            return DivergenceReason.DateOutOfTolerance;

        var sameDateDifferentAmount = counterparts.Any(c =>
            SameCurrency(transaction, c)
            && c.TransactionDate.Date == transaction.TransactionDate.Date
            && c.Amount != transaction.Amount);

        if (sameDateDifferentAmount || pendingCandidates.Count == 1)
            return DivergenceReason.AmountOutOfTolerance;

        return DivergenceReason.NoCandidate;
    }

    private static bool SameCurrency(CanonicalTransaction a, CanonicalTransaction b) =>
        string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.OrdinalIgnoreCase);
}
