using System.Net;
using System.Net.Http.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Tests.Api;

public class AuthenticationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthenticationTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_request()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/transactions?companyId=" + Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_stays_public()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_usable_token()
    {
        var user = _factory.AddUser(UserRole.Admin, "SenhaForte@123");

        var client = await _factory.CreateAuthenticatedClientAsync(user, "SenhaForte@123");
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(user.Id, body!.UserId);
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var user = _factory.AddUser(UserRole.Admin, "SenhaForte@123");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "senha-errada" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_gives_the_same_generic_error()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "ninguem@teste.com", password = "qualquer-senha" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("E-mail ou senha inválidos.", problem!.Detail);
    }

    [Fact]
    public async Task Inactive_user_cannot_log_in()
    {
        var user = _factory.AddUser(UserRole.Admin, "SenhaForte@123");
        _factory.SeedDatabase((context, _) =>
        {
            var stored = context.Users.Single(u => u.Id == user.Id);
            stored.Active = false;
        });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = "SenhaForte@123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_create_users()
    {
        var approver = _factory.AddUser(UserRole.Approver, "SenhaForte@123");
        var client = await _factory.CreateAuthenticatedClientAsync(approver, "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Novo", email = "novo@teste.com", password = "OutraSenha@123", role = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_users_and_password_hash_is_never_returned()
    {
        var admin = _factory.AddUser(UserRole.Admin, "SenhaForte@123");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Novo Analista",
            email = $"{Guid.NewGuid():N}@teste.com",
            password = "OutraSenha@123",
            role = (int)UserRole.Analyst
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pbkdf2", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_payload_is_rejected_with_validation_error()
    {
        var admin = _factory.AddUser(UserRole.Admin, "SenhaForte@123");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            name = "X", email = "nao-e-email", password = "123", role = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_admin_is_flagged_to_change_the_initial_password()
    {
        var admin = _factory.AddUser(UserRole.Admin, "SenhaInicial@123");
        _factory.SeedDatabase((context, _) =>
        {
            var stored = context.Users.Single(u => u.Id == admin.Id);
            stored.MustChangePassword = true;
        });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = admin.Email, password = "SenhaInicial@123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiTestFactory.LoginPayload>();
        Assert.True(payload!.MustChangePassword);
    }

    private record MeResponse(Guid UserId, string Role, Guid? CompanyId);
    private record ProblemResponse(string Title, string Detail, int Status);
}
