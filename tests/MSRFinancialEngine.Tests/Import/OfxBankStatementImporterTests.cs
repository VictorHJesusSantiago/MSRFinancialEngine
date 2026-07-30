using System.Text;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Tests.Import;

public class OfxBankStatementImporterTests
{
    [Fact]
    public void Parses_stmttrn_blocks()
    {
        var ofx = """
        <OFX>
        <BANKMSGSRSV1>
        <STMTTRNRS>
        <STMTRS>
        <BANKTRANLIST>
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20260110120000[0:GMT]
        <TRNAMT>-150.75
        <FITID>FIT001
        <NAME>Fornecedor Acme
        <MEMO>Pagamento mensal
        </STMTTRN>
        </BANKTRANLIST>
        </STMTRS>
        </STMTTRNRS>
        </BANKMSGSRSV1>
        </OFX>
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ofx));
        var importer = new OfxBankStatementImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Single(result);
        Assert.Equal(-150.75m, result[0].Amount);
        Assert.Equal(new DateTime(2026, 1, 10), result[0].TransactionDate);
        Assert.Equal("Pagamento mensal", result[0].Description);
        Assert.Equal("FIT001", result[0].ReferenceDoc);
    }
}
