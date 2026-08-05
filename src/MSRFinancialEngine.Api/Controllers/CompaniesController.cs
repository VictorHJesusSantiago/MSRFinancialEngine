using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateCompanyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(32)] string TaxId,
    [Required, StringLength(3, MinimumLength = 3)] string BaseCurrencyCode);

public record UpdateCompanyRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(32)] string TaxId,
    [Required, StringLength(3, MinimumLength = 3)] string BaseCurrencyCode);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly IRepository<Company> _companies;
    private readonly IUnitOfWork _unitOfWork;

    public CompaniesController(IRepository<Company> companies, IUnitOfWork unitOfWork)
    {
        _companies = companies;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public ActionResult<PagedResult<Company>> GetAll([FromQuery] PageRequest pagination) =>
        Ok(_companies.Query().OrderBy(c => c.Name).ToPagedResult(pagination));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Company>> GetById(Guid id, CancellationToken ct)
    {
        var company = await _companies.GetByIdAsync(id, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async Task<ActionResult<Company>> Create(CreateCompanyRequest request, CancellationToken ct)
    {
        var company = new Company
        {
            Name = request.Name,
            TaxId = request.TaxId,
            BaseCurrencyCode = request.BaseCurrencyCode
        };

        await _companies.AddAsync(company, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = company.Id }, company);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Company>> Update(Guid id, UpdateCompanyRequest request, CancellationToken ct)
    {
        var company = await _companies.GetByIdAsync(id, ct);
        if (company is null)
            return NotFound();

        company.Name = request.Name;
        company.TaxId = request.TaxId;
        company.BaseCurrencyCode = request.BaseCurrencyCode.ToUpperInvariant();

        _companies.Update(company);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(company);
    }
}
