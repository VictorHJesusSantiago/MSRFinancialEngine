using System.Text.Json;
using System.Text.Json.Serialization;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

/// <summary>
/// Importador para exportações de ERP em JSON: array de objetos
/// {"amount":100.0,"currency":"BRL","date":"2026-07-01","description":"...","reference":"...","account":"..."}.
/// </summary>
public class ErpJsonImporter : ISourceImporter
{
    public SourceType SupportedType => SourceType.ErpJson;

    public IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<List<ErpTransactionDto>>(content, options) ?? new();

        return items.Select(i => new RawImportedTransaction
        {
            Amount = i.Amount,
            CurrencyCode = (i.Currency ?? "BRL").ToUpperInvariant(),
            TransactionDate = i.Date,
            Description = i.Description ?? string.Empty,
            ReferenceDoc = i.Reference,
            AccountIdentifier = i.Account,
            OriginalPayloadJson = JsonSerializer.Serialize(i)
        }).ToList();
    }

    private class ErpTransactionDto
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public string? Account { get; set; }
    }
}
