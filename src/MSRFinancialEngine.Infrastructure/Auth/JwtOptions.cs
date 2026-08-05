namespace MSRFinancialEngine.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MSRFinancialEngine";
    public string Audience { get; set; } = "MSRFinancialEngine";

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}

public class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Administrador";
    public string Email { get; set; } = "admin@msrfinancialengine.local";
    public string Password { get; set; } = string.Empty;
}
