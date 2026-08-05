using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly IRepository<CanonicalTransaction> _transactions;

    public TransactionsController(IRepository<CanonicalTransaction> transactions)
    {
        _transactions = transactions;
    }

    [HttpGet]
    public ActionResult<PagedResult<CanonicalTransaction>> GetAll(
        [FromQuery] Guid companyId, [FromQuery] bool? reconciled, [FromQuery] PageRequest pagination)
    {
        var query = _transactions.Query().Where(t => t.CompanyId == companyId);
        if (reconciled.HasValue)
            query = query.Where(t => t.Reconciled == reconciled.Value);

        return Ok(query.OrderByDescending(t => t.TransactionDate).ToPagedResult(pagination));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CanonicalTransaction>> GetById(Guid id, CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(id, ct);
        return transaction is null ? NotFound() : Ok(transaction);
    }
}
