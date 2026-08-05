using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateUserRequest(
    [Required, MaxLength(200)] string Name,
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MinLength(8)] string Password,
    [Required] UserRole Role,
    [Range(0, 999_999_999)] decimal? ApprovalLimitAmount,
    Guid? CompanyId);

public record ResetPasswordRequest([Required, MinLength(8)] string NewPassword);

public record UserResponse(Guid Id, string Name, string Email, string Role, decimal? ApprovalLimitAmount, Guid? CompanyId, bool Active);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IRepository<ApplicationUser> _users;
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public UsersController(IRepository<ApplicationUser> users, IAuthService authService, ICurrentUser currentUser)
    {
        _users = users;
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public ActionResult<PagedResult<UserResponse>> GetAll([FromQuery] PageRequest pagination) =>
        Ok(_users.Query().OrderBy(u => u.Name).Select(u => ToResponse(u)).ToPagedResult(pagination));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var id = await _authService.CreateUserAsync(new CreateUserCommand
        {
            Name = request.Name,
            Email = request.Email,
            Password = request.Password,
            Role = request.Role,
            ApprovalLimitAmount = request.ApprovalLimitAmount,
            CompanyId = request.CompanyId
        }, ct);

        var created = await _users.GetByIdAsync(id, ct)!;
        return CreatedAtAction(nameof(GetById), new { id }, ToResponse(created!));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _authService.DeactivateUserAsync(id, _currentUser.RequireUserId(), ct);
        return NoContent();
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _authService.ReactivateUserAsync(id, _currentUser.RequireUserId(), ct);
        return NoContent();
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(id, request.NewPassword, _currentUser.RequireUserId(), ct);
        return NoContent();
    }

    private static UserResponse ToResponse(ApplicationUser u) =>
        new(u.Id, u.Name, u.Email, u.Role.ToString(), u.ApprovalLimitAmount, u.CompanyId, u.Active);
}
