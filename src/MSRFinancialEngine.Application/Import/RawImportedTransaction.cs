namespace MSRFinancialEngine.Application.Import;

/// <summary>
/// Resultado intermediário de um parser de fonte: dados já lidos do formato de origem,
/// mas ainda não persistidos nem normalizados para o modelo canônico.
/// </summary>
public class RawImportedTransaction
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceDoc { get; set; }
    public string? AccountIdentifier { get; set; }

    /// <summary>Payload original serializado, preservado em RawTransaction.PayloadJson.</summary>
    public string OriginalPayloadJson { get; set; } = "{}";
}
