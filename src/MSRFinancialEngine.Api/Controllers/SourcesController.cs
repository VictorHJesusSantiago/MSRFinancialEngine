using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateSourceRequest(
    [Required] Guid CompanyId,
    [Required, MaxLength(200)] string Name,
    [Required] SourceType Type,
    string ConfigJson);

public record UpdateSourceRequest(
    [Required, MaxLength(200)] string Name,
    string ConfigJson,
    bool Active);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly IRepository<Source> _sources;
    private readonly IUnitOfWork _unitOfWork;

    public SourcesController(IRepository<Source> sources, IUnitOfWork unitOfWork)
    {
        _sources = sources;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public ActionResult<PagedResult<Source>> GetAll([FromQuery] Guid? companyId, [FromQuery] PageRequest pagination)
    {
        var query = _sources.Query();
        if (companyId.HasValue)
            query = query.Where(s => s.CompanyId == companyId.Value);

        return Ok(query.OrderBy(s => s.Name).ToPagedResult(pagination));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Source>> GetById(Guid id, CancellationToken ct)
    {
        var source = await _sources.GetByIdAsync(id, ct);
        return source is null ? NotFound() : Ok(source);
    }

    [HttpPost]
    public async Task<ActionResult<Source>> Create(CreateSourceRequest request, CancellationToken ct)
    {
        var source = new Source
        {
            CompanyId = request.CompanyId,
            Name = request.Name,
            Type = request.Type,
            ConfigJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson
        };

        await _sources.AddAsync(source, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = source.Id }, source);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Source>> Update(Guid id, UpdateSourceRequest request, CancellationToken ct)
    {
        var source = await _sources.GetByIdAsync(id, ct);
        if (source is null)
            return NotFound();

        source.Name = request.Name;
        source.ConfigJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson;
        source.Active = request.Active;

        _sources.Update(source);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(source);
    }
}
