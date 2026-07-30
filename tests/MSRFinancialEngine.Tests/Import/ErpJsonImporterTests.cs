using System.Text;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Tests.Import;

public class ErpJsonImporterTests
{
    [Fact]
    public void Parses_json_array_into_raw_transactions()
    {
        var json = """
        [
          {"amount": 200.00, "currency": "BRL", "date": "2026-01-05T00:00:00", "description": "Fatura 123", "reference": "FAT-123", "account": "ERP-1"},
          {"amount": -30.00, "currency": "BRL", "date": "2026-01-06T00:00:00", "description": "Estorno"}
        ]
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var importer = new ErpJsonImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Equal(2, result.Count);
        Assert.Equal(200.00m, result[0].Amount);
        Assert.Equal("FAT-123", result[0].ReferenceDoc);
        Assert.Equal("ERP-1", result[0].AccountIdentifier);
        Assert.Null(result[1].ReferenceDoc);
    }
}
