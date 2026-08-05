using System.Net;
using System.Net.Http.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Tests.Api;

public class ChangePasswordTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ChangePasswordTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_can_change_their_own_password_and_log_in_with_the_new_one()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaAntiga@123");

        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "SenhaAntiga@123", newPassword = "SenhaNova@456" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var anonymous = _factory.CreateClient();

        var withOld = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaAntiga@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);

        var withNew = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaNova@456" });
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_revokes_existing_sessions()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var anonymous = _factory.CreateClient();

        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaAntiga@123" });
        var tokens = await login.Content.ReadFromJsonAsync<TokenPayload>();

        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaAntiga@123");
        await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "SenhaAntiga@123", newPassword = "SenhaNova@456" });

        var refresh = await anonymous.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = tokens!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Wrong_current_password_is_rejected()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaAntiga@123");

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "chute-errado", newPassword = "SenhaNova@456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task New_password_must_differ_from_the_current_one()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaAntiga@123");

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "SenhaAntiga@123", newPassword = "SenhaAntiga@123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_without_digits_is_rejected_by_the_policy()
    {
        var user = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaAntiga@123");

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "SenhaAntiga@123", newPassword = "apenasletrasaqui" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_change_a_password()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "qualquer", newPassword = "OutraSenha@123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record TokenPayload(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc);
}
