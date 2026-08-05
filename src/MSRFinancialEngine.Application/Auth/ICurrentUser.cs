using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }

    Guid RequireUserId();
}

public class AuthenticationRequiredException : InvalidOperationException
{
    public AuthenticationRequiredException(string message) : base(message)
    {
    }
}
