using System.Globalization;
using System.Text.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public class CsvBankStatementImporter : ISourceImporter
{
    public SourceType SupportedType => SourceType.BankStatementCsv;

    public IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson)
    {
        var config = JsonSerializer.Deserialize<CsvImporterConfig>(configJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CsvImporterConfig();

        var results = new List<RawImportedTransaction>();
        using var reader = new StreamReader(content);

        string? headerLine = config.HasHeader ? reader.ReadLine() : null;
        var columnIndex = ResolveColumnIndex(headerLine, config.Delimiter);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitCsvLine(line, config.Delimiter);

            string Get(string col, int fallbackIndex)
            {
                var idx = columnIndex.TryGetValue(col, out var i) ? i : fallbackIndex;
                return idx >= 0 && idx < fields.Count ? fields[idx].Trim() : string.Empty;
            }

            var dateRaw = Get("Date", 0);
            var amountRaw = Get("Amount", 1);
            var currencyRaw = Get("Currency", 2);
            var description = Get("Description", 3);
            var reference = Get("Reference", 4);
            var account = Get("Account", 5);

            if (string.IsNullOrWhiteSpace(dateRaw) || string.IsNullOrWhiteSpace(amountRaw))
                continue;

            var date = DateTime.ParseExact(dateRaw, config.DateFormat, CultureInfo.InvariantCulture);
            var amount = decimal.Parse(amountRaw, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var currency = string.IsNullOrWhiteSpace(currencyRaw) ? config.DefaultCurrency : currencyRaw;

            results.Add(new RawImportedTransaction
            {
                Amount = amount,
                CurrencyCode = currency.ToUpperInvariant(),
                TransactionDate = date,
                Description = description,
                ReferenceDoc = string.IsNullOrWhiteSpace(reference) ? null : reference,
                AccountIdentifier = string.IsNullOrWhiteSpace(account) ? null : account,
                OriginalPayloadJson = JsonSerializer.Serialize(new { line, fields })
            });
        }

        return results;
    }

    private static Dictionary<string, int> ResolveColumnIndex(string? headerLine, string delimiter)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(headerLine))
            return map;

        var headers = headerLine.Split(delimiter);
        for (var i = 0; i < headers.Length; i++)
            map[headers[i].Trim()] = i;

        return map;
    }

    private static List<string> SplitCsvLine(string line, string delimiter)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && delimiter.Length == 1 && c == delimiter[0])
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result;
    }
}

public class CsvImporterConfig
{
    public string Delimiter { get; set; } = ",";
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public bool HasHeader { get; set; } = true;
    public string DefaultCurrency { get; set; } = "BRL";
}
