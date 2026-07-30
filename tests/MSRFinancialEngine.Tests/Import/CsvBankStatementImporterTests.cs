using System.Text;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Tests.Import;

public class CsvBankStatementImporterTests
{
    [Fact]
    public void Parses_csv_with_header_into_raw_transactions()
    {
        var csv = "Date,Amount,Currency,Description,Reference,Account\n" +
                  "2026-01-10,100.50,BRL,Pagamento Fornecedor,NF-001,CC-123\n" +
                  "2026-01-11,-50.00,BRL,Tarifa Bancaria,,CC-123\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var importer = new CsvBankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Equal(2, result.Count);
        Assert.Equal(100.50m, result[0].Amount);
        Assert.Equal("BRL", result[0].CurrencyCode);
        Assert.Equal("NF-001", result[0].ReferenceDoc);
        Assert.Equal(new DateTime(2026, 1, 10), result[0].TransactionDate);
        Assert.Equal(-50.00m, result[1].Amount);
        Assert.Null(result[1].ReferenceDoc);
    }

    [Fact]
    public void Uses_default_currency_when_column_is_empty()
    {
        var csv = "Date,Amount,Currency,Description,Reference,Account\n" +
                  "2026-01-10,100.50,,Pagamento,,\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var importer = new CsvBankStatementImporter();

        var result = importer.Parse(stream, "{\"defaultCurrency\":\"USD\"}");

        Assert.Single(result);
        Assert.Equal("USD", result[0].CurrencyCode);
    }
}
