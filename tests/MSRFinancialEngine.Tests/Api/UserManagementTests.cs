using System.Net;
using System.Net.Http.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Tests.Api;

public class UserManagementTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public UserManagementTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Deactivated_user_can_no_longer_log_in()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var target = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");

        var response = await client.PostAsync($"/api/users/{target.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = target.Email, password = "Senha@12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Deactivating_revokes_the_active_session_immediately()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var target = _factory.AddUser(UserRole.Approver, "Senha@12345");

        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = target.Email, password = "Senha@12345" });
        var tokens = await login.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();

        var adminClient = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");
        await adminClient.PostAsync($"/api/users/{target.Id}/deactivate", null);

        var refresh = await anonymous.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = tokens!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reactivated_user_can_log_in_again()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var target = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");

        await client.PostAsync($"/api/users/{target.Id}/deactivate", null);
        await client.PostAsync($"/api/users/{target.Id}/reactivate", null);

        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = target.Email, password = "Senha@12345" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_themselves()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");

        var response = await client.PostAsync($"/api/users/{admin.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_deactivate_anyone()
    {
        var approver = _factory.AddUser(UserRole.Approver, "Senha@12345");
        var target = _factory.AddUser(UserRole.Analyst, "Senha@12345");
        var client = await _factory.CreateAuthenticatedClientAsync(approver, "Senha@12345");

        var response = await client.PostAsync($"/api/users/{target.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_forces_a_change_on_next_login()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var target = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");

        var reset = await client.PostAsJsonAsync($"/api/users/{target.Id}/reset-password",
            new { newPassword = "SenhaProvisoria@9" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = target.Email, password = "SenhaProvisoria@9" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var payload = await login.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();
        Assert.True(payload!.MustChangePassword);
    }

    [Fact]
    public async Task Password_reset_unlocks_an_account_locked_by_failed_attempts()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var target = _factory.AddUser(UserRole.Approver, "SenhaAntiga@123");
        var anonymous = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await anonymous.PostAsJsonAsync("/api/auth/login",
                new { email = target.Email, password = "errada" });

        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");
        await client.PostAsJsonAsync($"/api/users/{target.Id}/reset-password",
            new { newPassword = "SenhaProvisoria@9" });

        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = target.Email, password = "SenhaProvisoria@9" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
