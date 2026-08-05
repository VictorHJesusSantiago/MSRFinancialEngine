using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Matching;

public static class AmountComparer
{
    public static decimal? Difference(
        MatchingContext context,
        CanonicalTransaction a,
        CanonicalTransaction b,
        bool matchOppositeSigns,
        bool crossCurrency)
    {
        decimal valueA;
        decimal valueB;

        if (string.Equals(a.CurrencyCode, b.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            valueA = a.Amount;
            valueB = b.Amount;
        }
        else
        {
            if (!crossCurrency)
                return null;

            var baseA = context.BaseAmountOf(a);
            var baseB = context.BaseAmountOf(b);

            if (baseA is null || baseB is null)
                return null;

            valueA = baseA.Value;
            valueB = baseB.Value;
        }

        return Difference(valueA, valueB, matchOppositeSigns);
    }

    public static decimal? Difference(decimal a, decimal b, bool matchOppositeSigns)
    {
        if (!matchOppositeSigns)
            return Math.Abs(a - b);

        if (a == 0m || b == 0m || Math.Sign(a) == Math.Sign(b))
            return null;

        return Math.Abs(Math.Abs(a) - Math.Abs(b));
    }
}
