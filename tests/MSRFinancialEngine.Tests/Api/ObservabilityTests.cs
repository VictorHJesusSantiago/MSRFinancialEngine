using System.Net;
using MSRFinancialEngine.Api.Middleware;

namespace MSRFinancialEngine.Tests.Api;

public class ObservabilityTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ObservabilityTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues(RequestContextMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task Correlation_id_from_the_caller_is_preserved()
    {
        var client = _factory.CreateClient();
        var provided = $"pedido-{Guid.NewGuid():N}";

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(RequestContextMiddleware.HeaderName, provided);

        var response = await client.SendAsync(request);

        Assert.Equal(provided, response.Headers.GetValues(RequestContextMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Correlation_id_is_present_even_on_failures()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/companies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains(RequestContextMiddleware.HeaderName));
    }

    [Fact]
    public async Task Absurdly_long_correlation_id_is_replaced_instead_of_echoed()
    {
        var client = _factory.CreateClient();
        var abusive = new string('x', 500);

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(RequestContextMiddleware.HeaderName, abusive);

        var response = await client.SendAsync(request);

        var returned = response.Headers.GetValues(RequestContextMiddleware.HeaderName).Single();
        Assert.NotEqual(abusive, returned);
    }
}
