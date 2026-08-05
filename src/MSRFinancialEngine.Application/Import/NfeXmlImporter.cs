using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public class NfeXmlImporter : ISourceImporter
{
    private const string NfeNamespace = "http://www.portalfiscal.inf.br/nfe";

    public SourceType SupportedType => SourceType.InvoiceXmlNfe;

    public IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson)
    {
        var config = JsonSerializer.Deserialize<NfeImporterConfig>(configJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new NfeImporterConfig();

        var document = XDocument.Load(content);
        XNamespace ns = NfeNamespace;

        var invoices = document.Descendants(ns + "infNFe").ToList();
        if (invoices.Count == 0)
            invoices = document.Descendants("infNFe").ToList();

        var results = new List<RawImportedTransaction>();

        foreach (var invoice in invoices)
        {
            var ide = FindChild(invoice, "ide");
            var emit = FindChild(invoice, "emit");
            var total = FindChild(invoice, "total");
            var icmsTot = total is null ? null : FindChild(total, "ICMSTot");

            var amountRaw = icmsTot is null ? null : FindChild(icmsTot, "vNF")?.Value;
            if (string.IsNullOrWhiteSpace(amountRaw))
                continue;

            var amount = decimal.Parse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture);

            var dateRaw = (ide is null ? null : FindChild(ide, "dhEmi")?.Value)
                ?? (ide is null ? null : FindChild(ide, "dEmi")?.Value);
            var date = ParseNfeDate(dateRaw);

            var invoiceNumber = ide is null ? null : FindChild(ide, "nNF")?.Value;
            var issuerName = emit is null ? null : FindChild(emit, "xNome")?.Value;

            var accessKey = invoice.Attribute("Id")?.Value?.Replace("NFe", string.Empty, StringComparison.OrdinalIgnoreCase);

            var description = string.IsNullOrWhiteSpace(issuerName)
                ? $"NF-E {invoiceNumber}"
                : $"NF-E {invoiceNumber} {issuerName}";

            results.Add(new RawImportedTransaction
            {
                Amount = amount,
                CurrencyCode = config.DefaultCurrency.ToUpperInvariant(),
                TransactionDate = date,
                Description = description.Trim(),
                ReferenceDoc = string.IsNullOrWhiteSpace(invoiceNumber) ? accessKey : invoiceNumber,
                AccountIdentifier = accessKey,
                OriginalPayloadJson = JsonSerializer.Serialize(new { xml = invoice.ToString() })
            });
        }

        return results;
    }

    private static XElement? FindChild(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static DateTime ParseNfeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.MinValue;

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset))
            return offset.Date;

        return DateTime.ParseExact(raw[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

public class NfeImporterConfig
{
    public string DefaultCurrency { get; set; } = "BRL";
}
