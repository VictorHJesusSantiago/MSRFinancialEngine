using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateMatchingRuleRequest(
    [Required] Guid CompanyId,
    [Required, MaxLength(200)] string Name,
    [Required] MatchingRuleType Type,
    string ConfigJson,
    [Range(0, 10_000)] int Priority);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MatchingRulesController : ControllerBase
{
    private readonly IRepository<MatchingRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public MatchingRulesController(IRepository<MatchingRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public ActionResult<IEnumerable<MatchingRule>> GetAll([FromQuery] Guid companyId) =>
        Ok(_rules.Query().Where(r => r.CompanyId == companyId).OrderBy(r => r.Priority).ToList());

    [HttpPost]
    public async Task<ActionResult<MatchingRule>> Create(CreateMatchingRuleRequest request, CancellationToken ct)
    {
        var rule = new MatchingRule
        {
            CompanyId = request.CompanyId,
            Name = request.Name,
            Type = request.Type,
            ConfigJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson,
            Priority = request.Priority
        };

        await _rules.AddAsync(rule, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(rule);
    }
}

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MatchingController : ControllerBase
{
    private readonly IMatchingEngine _engine;

    public MatchingController(IMatchingEngine engine)
    {
        _engine = engine;
    }

    [HttpPost("run/{companyId:guid}")]
    public async Task<ActionResult<MatchingRunResult>> Run(Guid companyId, CancellationToken ct) =>
        Ok(await _engine.RunForCompanyAsync(companyId, ct));
}
