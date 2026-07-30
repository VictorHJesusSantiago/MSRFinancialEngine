using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Api.Controllers;

public record CreateExchangeRateRequest(string CurrencyCode, string BaseCurrencyCode, DateOnly Date, decimal RateToBase);
public record ConvertCurrencyRequest(decimal Amount, string CurrencyCode, string BaseCurrencyCode, DateOnly Date);

[ApiController]
[Route("api/[controller]")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IRepository<ExchangeRate> _rates;
    private readonly ICurrencyConversionService _conversionService;
    private readonly IUnitOfWork _unitOfWork;

    public ExchangeRatesController(IRepository<ExchangeRate> rates, ICurrencyConversionService conversionService, IUnitOfWork unitOfWork)
    {
        _rates = rates;
        _conversionService = conversionService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ExchangeRate>> GetAll([FromQuery] string? currencyCode) =>
        Ok(_rates.Query().Where(r => currencyCode == null || r.CurrencyCode == currencyCode).OrderByDescending(r => r.Date).ToList());

    [HttpPost]
    public async Task<ActionResult<ExchangeRate>> Create(CreateExchangeRateRequest request, CancellationToken ct)
    {
        var rate = new ExchangeRate
        {
            CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
            BaseCurrencyCode = request.BaseCurrencyCode.ToUpperInvariant(),
            Date = request.Date,
            RateToBase = request.RateToBase
        };

        await _rates.AddAsync(rate, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(rate);
    }

    [HttpPost("convert")]
    public async Task<ActionResult<decimal>> Convert(ConvertCurrencyRequest request, CancellationToken ct) =>
        Ok(await _conversionService.ConvertToBaseAsync(request.Amount, request.CurrencyCode, request.BaseCurrencyCode, request.Date, ct));
}
