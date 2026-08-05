using System.Text;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Tests.Import;

public class NfeXmlImporterTests
{
    private const string SingleNfe = """
    <?xml version="1.0" encoding="UTF-8"?>
    <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
      <NFe>
        <infNFe Id="NFe35260114200166000187550010000000151234567890" versao="4.00">
          <ide>
            <nNF>15</nNF>
            <dhEmi>2026-01-10T09:30:00-03:00</dhEmi>
          </ide>
          <emit>
            <xNome>Fornecedor Acme Ltda</xNome>
          </emit>
          <total>
            <ICMSTot>
              <vNF>1250.75</vNF>
            </ICMSTot>
          </total>
        </infNFe>
      </NFe>
    </nfeProc>
    """;

    [Fact]
    public void Parses_single_nfe_with_namespace()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SingleNfe));
        var importer = new NfeXmlImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Single(result);
        Assert.Equal(1250.75m, result[0].Amount);
        Assert.Equal("BRL", result[0].CurrencyCode);
        Assert.Equal(new DateTime(2026, 1, 10), result[0].TransactionDate);
        Assert.Equal("15", result[0].ReferenceDoc);
        Assert.Contains("Fornecedor Acme Ltda", result[0].Description);
        Assert.Equal("35260114200166000187550010000000151234567890", result[0].AccountIdentifier);
    }

    [Fact]
    public void Parses_batch_with_multiple_invoices()
    {
        var batch = """
        <?xml version="1.0" encoding="UTF-8"?>
        <lote xmlns="http://www.portalfiscal.inf.br/nfe">
          <NFe><infNFe Id="NFe111"><ide><nNF>1</nNF><dhEmi>2026-02-01T10:00:00-03:00</dhEmi></ide>
            <emit><xNome>Emissor Um</xNome></emit>
            <total><ICMSTot><vNF>100.00</vNF></ICMSTot></total></infNFe></NFe>
          <NFe><infNFe Id="NFe222"><ide><nNF>2</nNF><dhEmi>2026-02-02T10:00:00-03:00</dhEmi></ide>
            <emit><xNome>Emissor Dois</xNome></emit>
            <total><ICMSTot><vNF>200.00</vNF></ICMSTot></total></infNFe></NFe>
        </lote>
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(batch));
        var importer = new NfeXmlImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Equal(2, result.Count);
        Assert.Equal(100.00m, result[0].Amount);
        Assert.Equal(200.00m, result[1].Amount);
        Assert.Equal("2", result[1].ReferenceDoc);
    }

    [Fact]
    public void Supports_legacy_dEmi_date_format()
    {
        var legacy = """
        <NFe xmlns="http://www.portalfiscal.inf.br/nfe">
          <infNFe Id="NFe999"><ide><nNF>99</nNF><dEmi>2026-03-15</dEmi></ide>
            <emit><xNome>Emissor Legado</xNome></emit>
            <total><ICMSTot><vNF>50.25</vNF></ICMSTot></total></infNFe>
        </NFe>
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacy));
        var importer = new NfeXmlImporter();

        var result = importer.Parse(stream, "{}");

        Assert.Single(result);
        Assert.Equal(new DateTime(2026, 3, 15), result[0].TransactionDate);
    }
}
