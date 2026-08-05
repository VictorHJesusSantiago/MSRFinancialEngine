using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MSRFinancialEngine.Application.Auth;

namespace MSRFinancialEngine.Api.Controllers;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record TokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    Guid UserId,
    string Name,
    string Role,
    Guid? CompanyId,
    bool MustChangePassword);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(ToResponse(await _authService.LoginAsync(request.Email, request.Password, ct)));

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(ToResponse(await _authService.RefreshAsync(request.RefreshToken, ct)));

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await _authService.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _authService.ChangePasswordAsync(
            _currentUser.RequireUserId(), request.CurrentPassword, request.NewPassword, ct);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult Me() => Ok(new
    {
        UserId = _currentUser.UserId,
        Role = _currentUser.Role?.ToString(),
        CompanyId = _currentUser.CompanyId
    });

    private static TokenResponse ToResponse(LoginResult result) => new(
        result.AccessToken,
        result.ExpiresAtUtc,
        result.RefreshToken,
        result.RefreshExpiresAtUtc,
        result.User.UserId,
        result.User.Name,
        result.User.Role.ToString(),
        result.User.CompanyId,
        result.User.MustChangePassword);
}
