using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public partial class OfxBankStatementImporter : ISourceImporter
{
    public SourceType SupportedType => SourceType.BankStatementOfx;

    public IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson)
    {
        var config = JsonSerializer.Deserialize<OfxImporterConfig>(configJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OfxImporterConfig();

        using var reader = new StreamReader(content);
        var text = reader.ReadToEnd();

        var results = new List<RawImportedTransaction>();
        foreach (Match block in StmtTrnRegex().Matches(text))
        {
            var body = block.Groups[1].Value;

            var amountRaw = ExtractTag(body, "TRNAMT");
            var dateRaw = ExtractTag(body, "DTPOSTED");
            var memo = ExtractTag(body, "MEMO");
            var name = ExtractTag(body, "NAME");
            var fitId = ExtractTag(body, "FITID");

            if (string.IsNullOrWhiteSpace(amountRaw) || string.IsNullOrWhiteSpace(dateRaw))
                continue;

            var amount = decimal.Parse(amountRaw, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var date = ParseOfxDate(dateRaw);
            var description = !string.IsNullOrWhiteSpace(memo) ? memo : name;

            results.Add(new RawImportedTransaction
            {
                Amount = amount,
                CurrencyCode = config.DefaultCurrency.ToUpperInvariant(),
                TransactionDate = date,
                Description = description,
                ReferenceDoc = string.IsNullOrWhiteSpace(fitId) ? null : fitId,
                OriginalPayloadJson = JsonSerializer.Serialize(new { body })
            });
        }

        return results;
    }

    private static string ExtractTag(string body, string tag)
    {
        var match = Regex.Match(body, $@"<{tag}>([^<\r\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static DateTime ParseOfxDate(string raw)
    {
        var digits = new string(raw.TakeWhile(char.IsDigit).ToArray());
        var datePart = digits.Length >= 8 ? digits[..8] : digits;
        return DateTime.ParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline)]
    private static partial Regex StmtTrnRegex();
}

public class OfxImporterConfig
{
    public string DefaultCurrency { get; set; } = "BRL";
}
