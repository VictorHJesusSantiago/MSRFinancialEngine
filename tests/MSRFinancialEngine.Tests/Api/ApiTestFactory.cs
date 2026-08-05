using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Api;

public class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:Postgres", "Host=unused");
        builder.UseSetting("Jwt:SigningKey", "chave-de-teste-com-mais-de-32-bytes-para-hmac");
        builder.UseSetting("Jwt:Issuer", "MSRFinancialEngine");
        builder.UseSetting("Jwt:Audience", "MSRFinancialEngine");
        builder.UseSetting("SeedAdmin:Enabled", "false");
        builder.UseSetting("Auth:LoginRateLimitPerMinute", "10000");

        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<FinancialEngineDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)
                            || d.ServiceType == typeof(FinancialEngineDbContext))
                .ToList();

            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<FinancialEngineDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IHostedService>();
        });
    }

    public void SeedDatabase(Action<FinancialEngineDbContext, IPasswordHasher> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinancialEngineDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        seed(context, hasher);
        context.SaveChanges();
    }

    public ApplicationUser AddUser(UserRole role, string password, Guid? companyId = null, decimal? limit = null)
    {
        ApplicationUser? created = null;

        SeedDatabase((context, hasher) =>
        {
            created = new ApplicationUser
            {
                Name = $"Usuário {role}",
                Email = $"{Guid.NewGuid():N}@teste.com",
                PasswordHash = hasher.Hash(password),
                Role = role,
                CompanyId = companyId,
                ApprovalLimitAmount = limit
            };
            context.Users.Add(created);
        });

        return created!;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(ApplicationUser user, string password)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);

        return client;
    }

    public record LoginPayload(
        string AccessToken,
        DateTime ExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshExpiresAtUtc,
        Guid UserId,
        string Name,
        string Role,
        Guid? CompanyId,
        bool MustChangePassword);
}

internal static class ServiceCollectionTestExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
