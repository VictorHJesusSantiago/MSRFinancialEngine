using MSRFinancialEngine.Application.Matching;

namespace MSRFinancialEngine.Tests.Matching;

public class StringSimilarityTests
{
    [Fact]
    public void Identical_strings_have_similarity_one()
    {
        Assert.Equal(1.0, StringSimilarity.NormalizedSimilarity("PAGAMENTO FORNECEDOR X", "PAGAMENTO FORNECEDOR X"));
    }

    [Fact]
    public void Completely_different_strings_have_low_similarity()
    {
        var similarity = StringSimilarity.NormalizedSimilarity("ABC", "ZZZZZZ");
        Assert.True(similarity < 0.3);
    }

    [Fact]
    public void Similar_strings_have_high_similarity()
    {
        var similarity = StringSimilarity.NormalizedSimilarity("PAGAMENTO FORNECEDOR ACME LTDA", "PAGTO FORNECEDOR ACME LTDA");
        Assert.True(similarity > 0.7, $"esperado > 0.7, obtido {similarity}");
    }
}
