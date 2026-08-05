using System.Text;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Tests.Import;

public class Mt940BankStatementImporterTests
{
    [Fact]
    public void Parses_debit_and_credit_transactions_with_signs()
    {
        var mt940 = string.Join('\n',
            ":20:STMT001",
            ":25:BANCO/12345-6",
            ":60F:C260101BRL1000,00",
            ":61:2601100110D100,50NTRFNONREF//NF-001",
            ":86:PAGAMENTO FORNECEDOR ACME",
            ":61:2601150115C250,00NTRFNONREF//REC-002",
            ":86:RECEBIMENTO CLIENTE ZETA",
            ":62F:C260131BRL1149,50");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mt940));
        var importer = new Mt940BankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Equal(2, result.Count);

        Assert.Equal(-100.50m, result[0].Amount);
        Assert.Equal(new DateTime(2026, 1, 10), result[0].TransactionDate);
        Assert.Equal("PAGAMENTO FORNECEDOR ACME", result[0].Description);
        Assert.Equal("NF-001", result[0].ReferenceDoc);
        Assert.Equal("BANCO/12345-6", result[0].AccountIdentifier);

        Assert.Equal(250.00m, result[1].Amount);
        Assert.Equal(new DateTime(2026, 1, 15), result[1].TransactionDate);
    }

    [Fact]
    public void Reversal_marker_inverts_the_sign()
    {
        var mt940 = string.Join('\n',
            ":25:CONTA-1",
            ":61:2601100110RD100,00NTRFNONREF//EST-001",
            ":86:ESTORNO DE DEBITO");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mt940));
        var importer = new Mt940BankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Single(result);
        Assert.Equal(100.00m, result[0].Amount);
    }

    [Fact]
    public void Appends_continuation_lines_to_description()
    {
        var mt940 = string.Join('\n',
            ":25:CONTA-1",
            ":61:2601100110D75,00NTRFNONREF//REF-9",
            ":86:PAGAMENTO PARCIAL",
            "REFERENTE AO CONTRATO 123");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mt940));
        var importer = new Mt940BankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Single(result);
        Assert.Equal("PAGAMENTO PARCIAL REFERENTE AO CONTRATO 123", result[0].Description);
    }

    [Fact]
    public void Transaction_without_description_tag_is_still_imported()
    {
        var mt940 = string.Join('\n',
            ":25:CONTA-1",
            ":61:2601100110D10,00NTRFNONREF//A",
            ":61:2601110111D20,00NTRFNONREF//B");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mt940));
        var importer = new Mt940BankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Equal(2, result.Count);
        Assert.Equal(-10.00m, result[0].Amount);
        Assert.Equal(-20.00m, result[1].Amount);
    }
}
