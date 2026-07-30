using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateUserRequest(string Name, string Email);

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IRepository<ApplicationUser> _users;
    private readonly IUnitOfWork _unitOfWork;

    public UsersController(IRepository<ApplicationUser> users, IUnitOfWork unitOfWork)
    {
        _users = users;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ApplicationUser>> GetAll() => Ok(_users.Query().ToList());

    [HttpPost]
    public async Task<ActionResult<ApplicationUser>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser { Name = request.Name, Email = request.Email };
        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(user);
    }
}
