using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Api.Middleware;

public class CompanyContextMiddleware
{
    public const string HeaderName = "X-Company-Id";

    private readonly RequestDelegate _next;

    public CompanyContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICompanyContext companyContext, ICurrentUser currentUser)
    {
        if (currentUser.CompanyId is { } tokenCompanyId)
        {
            companyContext.SetCompany(tokenCompanyId);
        }
        else if (currentUser.Role == UserRole.Admin
                 && context.Request.Headers.TryGetValue(HeaderName, out var value)
                 && Guid.TryParse(value.ToString(), out var requestedCompanyId))
        {
            companyContext.SetCompany(requestedCompanyId);
        }

        await _next(context);
    }
}
