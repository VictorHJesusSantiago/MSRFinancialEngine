using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Tests.Api;

public class ThrottledApiTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:LoginRateLimitPerMinute", "3");
    }
}

public class LoginRateLimitTests : IClassFixture<ThrottledApiTestFactory>
{
    private readonly ThrottledApiTestFactory _factory;

    public LoginRateLimitTests(ThrottledApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_attempts_beyond_the_limit_are_throttled()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email = $"alvo{i}@teste.com", password = "chute" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Throttling_does_not_affect_other_endpoints()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { email = "x@teste.com", password = "chute" });

        var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

}
