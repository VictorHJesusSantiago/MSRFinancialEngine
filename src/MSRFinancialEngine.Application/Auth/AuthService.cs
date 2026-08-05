using System.Security.Cryptography;
using System.Text;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Auth;

public class AuthenticatedUser
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public Guid? CompanyId { get; init; }
    public bool MustChangePassword { get; init; }
}

public interface ITokenIssuer
{
    (string Token, DateTime ExpiresAtUtc) Issue(AuthenticatedUser user);
}

public class LoginResult
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshExpiresAtUtc { get; init; }
    public required AuthenticatedUser User { get; init; }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("E-mail ou senha inválidos.")
    {
    }

    public InvalidCredentialsException(string message) : base(message)
    {
    }
}

public class AccountLockedException : Exception
{
    public AccountLockedException(DateTime until)
        : base($"Conta temporariamente bloqueada por excesso de tentativas. Tente novamente após {until:HH:mm:ss} UTC.")
    {
    }
}

public class AuthOptions
{
    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public int MinimumPasswordLength { get; set; } = 8;

    public int LoginRateLimitPerMinute { get; set; } = 10;
}

public class CreateUserCommand
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required UserRole Role { get; init; }
    public decimal? ApprovalLimitAmount { get; init; }
    public Guid? CompanyId { get; init; }
    public bool MustChangePassword { get; init; }
}

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<Guid> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default);

    Task DeactivateUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default);

    Task ReactivateUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default);

    Task ResetPasswordAsync(Guid userId, string newPassword, Guid actingUserId, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IRepository<ApplicationUser> _users;
    private readonly IRepository<RefreshToken> _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuthOptions _options;

    public AuthService(
        IRepository<ApplicationUser> users,
        IRepository<RefreshToken> refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenIssuer tokenIssuer,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        AuthOptions options)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var user = _users.Query().FirstOrDefault(u => u.Email == normalizedEmail);

        if (user is null)
            throw new InvalidCredentialsException();

        if (user.LockoutEndUtc is { } lockedUntil && lockedUntil > now)
            throw new AccountLockedException(lockedUntil);

        if (!user.Active || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, now, ct);
            throw new InvalidCredentialsException();
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        _users.Update(user);

        var (result, _) = await IssueSessionAsync(user, now, ct);

        await _auditService.LogAsync(nameof(ApplicationUser), user.Id, "Login", user.Id, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var hash = HashToken(refreshToken);

        var stored = _refreshTokens.Query().FirstOrDefault(t => t.TokenHash == hash)
            ?? throw new InvalidCredentialsException("Refresh token inválido.");

        if (!stored.IsActive(now))
        {
            if (stored.RevokedAtUtc is not null)
            {
                await RevokeAllSessionsAsync(stored.UserId, "Reuso de refresh token revogado", ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            throw new InvalidCredentialsException("Refresh token expirado ou revogado.");
        }

        var user = await _users.GetByIdAsync(stored.UserId, ct);
        if (user is null || !user.Active)
            throw new InvalidCredentialsException("Refresh token inválido.");

        var (result, successor) = await IssueSessionAsync(user, now, ct);

        stored.RevokedAtUtc = now;
        stored.RevokedReason = "Rotacionado";
        stored.ReplacedByTokenId = successor.Id;
        _refreshTokens.Update(stored);

        await _auditService.LogAsync(nameof(RefreshToken), stored.Id, "Refreshed", user.Id, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var stored = _refreshTokens.Query().FirstOrDefault(t => t.TokenHash == hash);

        if (stored is null || stored.RevokedAtUtc is not null)
            return;

        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.RevokedReason = "Logout";
        _refreshTokens.Update(stored);

        await _auditService.LogAsync(nameof(RefreshToken), stored.Id, "Logout", stored.UserId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var active = _refreshTokens.Query()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToList();

        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
            token.RevokedReason = reason;
            _refreshTokens.Update(token);
        }

        if (active.Count > 0)
            await _auditService.LogAsync(nameof(ApplicationUser), userId, "SessionsRevoked", userId,
                new { Count = active.Count, Reason = reason }, ct);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
            throw new InvalidCredentialsException("Senha atual incorreta.");

        EnsurePasswordPolicy(newPassword);

        if (_passwordHasher.Verify(newPassword, user.PasswordHash))
            throw new InvalidOperationException("A nova senha deve ser diferente da atual.");

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.MustChangePassword = false;
        _users.Update(user);

        await RevokeAllSessionsAsync(userId, "Troca de senha", ct);

        await _auditService.LogAsync(nameof(ApplicationUser), userId, "PasswordChanged", userId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        if (_users.Query().Any(u => u.Email == normalizedEmail))
            throw new InvalidOperationException($"Já existe um usuário com o e-mail '{normalizedEmail}'.");

        EnsurePasswordPolicy(command.Password);

        var user = new ApplicationUser
        {
            Name = command.Name,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(command.Password),
            Role = command.Role,
            ApprovalLimitAmount = command.ApprovalLimitAmount,
            CompanyId = command.CompanyId,
            MustChangePassword = command.MustChangePassword,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _auditService.LogAsync(nameof(ApplicationUser), user.Id, "Created", null,
            new { user.Email, Role = user.Role.ToString(), user.ApprovalLimitAmount, user.CompanyId }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return user.Id;
    }

    public async Task DeactivateUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        if (userId == actingUserId)
            throw new InvalidOperationException("Não é possível desativar o próprio usuário.");

        user.Active = false;
        _users.Update(user);

        await RevokeAllSessionsAsync(userId, "Usuário desativado", ct);

        await _auditService.LogAsync(nameof(ApplicationUser), userId, "Deactivated", actingUserId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ReactivateUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        user.Active = true;
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        _users.Update(user);

        await _auditService.LogAsync(nameof(ApplicationUser), userId, "Reactivated", actingUserId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, Guid actingUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        EnsurePasswordPolicy(newPassword);

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        _users.Update(user);

        await RevokeAllSessionsAsync(userId, "Senha redefinida por administrador", ct);

        await _auditService.LogAsync(nameof(ApplicationUser), userId, "PasswordReset", actingUserId, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task RegisterFailedAttemptAsync(ApplicationUser user, DateTime now, CancellationToken ct)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= _options.MaxFailedLoginAttempts)
        {
            user.LockoutEndUtc = now.AddMinutes(_options.LockoutMinutes);
            user.FailedLoginAttempts = 0;

            await _auditService.LogAsync(nameof(ApplicationUser), user.Id, "LockedOut", user.Id,
                new { Until = user.LockoutEndUtc }, ct);
        }

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<(LoginResult Result, RefreshToken Stored)> IssueSessionAsync(
        ApplicationUser user, DateTime now, CancellationToken ct)
    {
        var authenticated = ToAuthenticatedUser(user);
        var (accessToken, expiresAt) = _tokenIssuer.Issue(authenticated);

        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAt = now.AddDays(_options.RefreshTokenDays);

        var stored = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiresAt
        };
        await _refreshTokens.AddAsync(stored, ct);

        var result = new LoginResult
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAt,
            RefreshToken = refreshToken,
            RefreshExpiresAtUtc = refreshExpiresAt,
            User = authenticated
        };

        return (result, stored);
    }

    private void EnsurePasswordPolicy(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < _options.MinimumPasswordLength)
            throw new InvalidOperationException(
                $"A senha deve ter pelo menos {_options.MinimumPasswordLength} caracteres.");

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
            throw new InvalidOperationException("A senha deve conter letras e números.");
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static AuthenticatedUser ToAuthenticatedUser(ApplicationUser user) => new()
    {
        UserId = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        CompanyId = user.CompanyId,
        MustChangePassword = user.MustChangePassword
    };
}
