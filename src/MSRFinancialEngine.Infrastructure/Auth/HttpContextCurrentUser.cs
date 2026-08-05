using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Infrastructure.Auth;

public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = FindClaim(JwtRegisteredClaimNames.Sub) ?? FindClaim(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? CompanyId
    {
        get
        {
            var value = FindClaim(JwtTokenIssuer.CompanyClaimType);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var value = FindClaim(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(value, out var role) ? role : null;
        }
    }

    public Guid RequireUserId() => UserId
        ?? throw new AuthenticationRequiredException("A requisição precisa estar autenticada.");

    private string? FindClaim(string type) => Principal?.FindFirst(type)?.Value;
}
