namespace MSRFinancialEngine.Application.Import;

public class RawImportedTransaction
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceDoc { get; set; }
    public string? AccountIdentifier { get; set; }

    public string OriginalPayloadJson { get; set; } = "{}";
}
