using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record AssignDivergenceRequest(Guid UserId);
public record DecideDivergenceRequest(Guid UserId, ApprovalDecisionType Decision, Guid? MatchedTransactionId, string? Notes);

[ApiController]
[Route("api/[controller]")]
public class DivergencesController : ControllerBase
{
    private readonly IRepository<Divergence> _divergences;
    private readonly IApprovalWorkflowService _workflow;

    public DivergencesController(IRepository<Divergence> divergences, IApprovalWorkflowService workflow)
    {
        _divergences = divergences;
        _workflow = workflow;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Divergence>> GetAll([FromQuery] DivergenceStatus? status)
    {
        var query = _divergences.Query();
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return Ok(query.OrderBy(d => d.CreatedAt).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Divergence>> GetById(Guid id, CancellationToken ct)
    {
        var divergence = await _divergences.GetByIdAsync(id, ct);
        return divergence is null ? NotFound() : Ok(divergence);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignDivergenceRequest request, CancellationToken ct)
    {
        await _workflow.AssignAsync(id, request.UserId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/decide")]
    public async Task<ActionResult<Guid>> Decide(Guid id, DecideDivergenceRequest request, CancellationToken ct)
    {
        var decisionId = await _workflow.DecideAsync(id, request.UserId, request.Decision, request.MatchedTransactionId, request.Notes, ct);
        return Ok(decisionId);
    }
}
