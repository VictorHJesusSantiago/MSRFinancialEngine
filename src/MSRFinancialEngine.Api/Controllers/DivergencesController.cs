using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record AssignDivergenceRequest([Required] Guid UserId);

public record DecideDivergenceRequest(
    [Required] ApprovalDecisionType Decision,
    Guid? MatchedTransactionId,
    [MaxLength(1000)] string? Notes);

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DivergencesController : ControllerBase
{
    private readonly IRepository<Divergence> _divergences;
    private readonly IApprovalWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;

    public DivergencesController(
        IRepository<Divergence> divergences,
        IApprovalWorkflowService workflow,
        ICurrentUser currentUser)
    {
        _divergences = divergences;
        _workflow = workflow;
        _currentUser = currentUser;
    }

    [HttpGet]
    public ActionResult<PagedResult<Divergence>> GetAll(
        [FromQuery] DivergenceStatus? status, [FromQuery] PageRequest pagination)
    {
        var query = _divergences.Query();
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return Ok(query.OrderBy(d => d.CreatedAt).ToPagedResult(pagination));
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
        var decisionId = await _workflow.DecideAsync(
            id, _currentUser.RequireUserId(), request.Decision, request.MatchedTransactionId, request.Notes, ct);

        return Ok(decisionId);
    }
}
