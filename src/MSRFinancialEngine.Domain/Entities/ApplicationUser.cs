namespace MSRFinancialEngine.Domain.Entities;

public class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Active { get; set; } = true;

    public string PasswordHash { get; set; } = string.Empty;

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }

    public UserRole Role { get; set; } = UserRole.Analyst;

    public decimal? ApprovalLimitAmount { get; set; }

    public Guid? CompanyId { get; set; }
}
