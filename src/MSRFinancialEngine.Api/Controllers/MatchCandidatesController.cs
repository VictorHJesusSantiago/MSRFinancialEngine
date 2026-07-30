using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchCandidatesController : ControllerBase
{
    private readonly IRepository<MatchCandidate> _candidates;

    public MatchCandidatesController(IRepository<MatchCandidate> candidates)
    {
        _candidates = candidates;
    }

    [HttpGet]
    public ActionResult<IEnumerable<MatchCandidate>> GetAll(
        [FromQuery] Guid? transactionId, [FromQuery] MatchCandidateStatus? status)
    {
        var query = _candidates.Query();
        if (transactionId.HasValue)
            query = query.Where(c => c.TransactionAId == transactionId.Value || c.TransactionBId == transactionId.Value);
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return Ok(query.OrderByDescending(c => c.Score).ToList());
    }
}
