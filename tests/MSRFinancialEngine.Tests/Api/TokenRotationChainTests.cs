using System.Net.Http.Json;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Api;

public class TokenRotationChainTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public TokenRotationChainTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rotating_links_the_revoked_token_to_its_successor()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "Senha@12345" });
        var first = await login.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();

        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first!.RefreshToken });

        RefreshToken? revoked = null;
        RefreshToken? successor = null;

        _factory.SeedDatabase((context, _) =>
        {
            var tokens = context.RefreshTokens
                .Where(t => t.UserId == user.Id)
                .OrderBy(t => t.CreatedAtUtc)
                .ToList();

            revoked = tokens.First();
            successor = tokens.Last();
        });

        Assert.NotNull(revoked!.RevokedAtUtc);
        Assert.Equal("Rotacionado", revoked.RevokedReason);

        Assert.Equal(successor!.Id, revoked.ReplacedByTokenId);
    }

    [Fact]
    public async Task The_login_token_has_no_predecessor()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Senha@12345" });

        _factory.SeedDatabase((context, _) =>
        {
            var token = context.RefreshTokens.Single(t => t.UserId == user.Id);
            Assert.Null(token.ReplacedByTokenId);
            Assert.Null(token.RevokedAtUtc);
        });
    }

    [Fact]
    public async Task A_chain_of_rotations_stays_fully_linked()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "Senha@12345" });
        var current = await login.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = current!.RefreshToken });
            current = await response.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();
        }

        _factory.SeedDatabase((context, _) =>
        {
            var tokens = context.RefreshTokens
                .Where(t => t.UserId == user.Id)
                .OrderBy(t => t.CreatedAtUtc)
                .ToList();

            Assert.Equal(4, tokens.Count);

            for (var i = 0; i < tokens.Count - 1; i++)
                Assert.Equal(tokens[i + 1].Id, tokens[i].ReplacedByTokenId);

            Assert.Null(tokens[^1].ReplacedByTokenId);
        });
    }
}
