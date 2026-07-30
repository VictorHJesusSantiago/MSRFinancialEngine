using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    /// <summary>Importa e normaliza um arquivo (CSV/OFX/JSON conforme o tipo da fonte) enviado via multipart/form-data.</summary>
    [HttpPost("{sourceId:guid}")]
    public async Task<ActionResult<ImportResult>> Import(Guid sourceId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Arquivo não informado ou vazio.");

        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(sourceId, stream, ct);
        return Ok(result);
    }
}
