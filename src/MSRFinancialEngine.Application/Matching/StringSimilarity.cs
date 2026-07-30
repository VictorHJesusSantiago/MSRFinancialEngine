namespace MSRFinancialEngine.Application.Matching;

/// <summary>Distância de Levenshtein normalizada em similaridade de 0.0 (nada em comum) a 1.0 (idênticas).</summary>
public static class StringSimilarity
{
    public static double NormalizedSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0.0;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var lenA = a.Length;
        var lenB = b.Length;
        var dist = new int[lenA + 1, lenB + 1];

        for (var i = 0; i <= lenA; i++) dist[i, 0] = i;
        for (var j = 0; j <= lenB; j++) dist[0, j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            for (var j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dist[i, j] = Math.Min(
                    Math.Min(dist[i - 1, j] + 1, dist[i, j - 1] + 1),
                    dist[i - 1, j - 1] + cost);
            }
        }

        return dist[lenA, lenB];
    }
}
