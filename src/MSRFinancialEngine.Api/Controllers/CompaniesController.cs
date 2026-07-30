using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateCompanyRequest(string Name, string TaxId, string BaseCurrencyCode);

[ApiController]
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
    public ActionResult<IEnumerable<Company>> GetAll() => Ok(_companies.Query().ToList());

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
}
