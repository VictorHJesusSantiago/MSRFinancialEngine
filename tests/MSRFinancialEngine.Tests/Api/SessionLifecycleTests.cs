using System.Net;
using System.Net.Http.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Tests.Api;

public class SessionLifecycleTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public SessionLifecycleTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, TokenPayload tokens)> LoginAsync(UserRole role = UserRole.Admin)
    {
        var user = _factory.AddUser(role, "SenhaForte@123");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaForte@123" });
        response.EnsureSuccessStatusCode();

        return (client, (await response.Content.ReadFromJsonAsync<TokenPayload>())!);
    }

    [Fact]
    public async Task Login_returns_both_access_and_refresh_tokens()
    {
        var (_, tokens) = await LoginAsync();

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.RefreshExpiresAtUtc > tokens.ExpiresAtUtc);
    }

    [Fact]
    public async Task Refresh_exchanges_the_token_for_a_new_pair()
    {
        var (client, tokens) = await LoginAsync();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var renewed = await response.Content.ReadFromJsonAsync<TokenPayload>();
        Assert.NotEqual(tokens.RefreshToken, renewed!.RefreshToken);
    }

    [Fact]
    public async Task Rotated_refresh_token_cannot_be_used_again()
    {
        var (client, tokens) = await LoginAsync();

        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_revoked_token_kills_every_session_of_that_user()
    {
        var (client, first) = await LoginAsync();

        var second = await (await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = first.RefreshToken })).Content.ReadFromJsonAsync<TokenPayload>();

        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });

        var afterBreach = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = second!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterBreach.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (client, tokens) = await LoginAsync();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_is_idempotent()
    {
        var (client, tokens) = await LoginAsync();

        await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens.RefreshToken });
        var again = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures_even_with_the_right_password()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaForte@123");
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = "errada" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaForte@123" });

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        var problem = await locked.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Contains("bloqueada", problem!.Detail);
    }

    [Fact]
    public async Task Successful_login_clears_the_failure_counter()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaForte@123");
        var client = _factory.CreateClient();

        for (var i = 0; i < 4; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "errada" });

        var ok = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaForte@123" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        for (var i = 0; i < 4; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "errada" });

        var stillOk = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaForte@123" });
        Assert.Equal(HttpStatusCode.OK, stillOk.StatusCode);
    }

    private record TokenPayload(
        string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc,
        Guid UserId, string Name, string Role, Guid? CompanyId, bool MustChangePassword);

    private record ProblemPayload(string Title, string Detail, int Status);
}
