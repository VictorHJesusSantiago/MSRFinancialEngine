using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Infrastructure.Auth;

public class DatabaseSeeder
{
    private readonly FinancialEngineDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SeedAdminOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        FinancialEngineDbContext context,
        IPasswordHasher passwordHasher,
        IOptions<SeedAdminOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public void SeedAdminUser()
    {
        if (!_options.Enabled)
            return;

        if (_context.Users.Any())
            return;

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning(
                "Nenhum usuário cadastrado e SeedAdmin:Password não foi configurado. " +
                "Defina a senha para que o administrador inicial seja criado e o login seja possível.");
            return;
        }

        var admin = new Domain.Entities.ApplicationUser
        {
            Name = _options.Name,
            Email = _options.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(_options.Password),
            Role = UserRole.Admin,
            MustChangePassword = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        _context.Users.Add(admin);
        _context.SaveChanges();

        _logger.LogInformation("Administrador inicial criado: {Email}", admin.Email);
    }
}
