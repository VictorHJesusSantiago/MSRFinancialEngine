using System.Globalization;
using System.Text.Json;
using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public class Mt940BankStatementImporter : ISourceImporter
{
    public SourceType SupportedType => SourceType.BankStatementMt940;

    public IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson)
    {
        var config = JsonSerializer.Deserialize<Mt940ImporterConfig>(configJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Mt940ImporterConfig();

        using var reader = new StreamReader(content);
        var results = new List<RawImportedTransaction>();

        string? accountIdentifier = null;
        Mt940Statement? pending = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();

            if (line.StartsWith(":25:", StringComparison.Ordinal))
            {
                accountIdentifier = line[4..].Trim();
                continue;
            }

            if (line.StartsWith(":61:", StringComparison.Ordinal))
            {
                if (pending is not null)
                    results.Add(Build(pending, accountIdentifier, config));

                pending = ParseStatementLine(line[4..].Trim());
                continue;
            }

            if (line.StartsWith(":86:", StringComparison.Ordinal) && pending is not null)
            {
                pending.Description = line[4..].Trim();
                continue;
            }

            if (pending is not null && !line.StartsWith(':') && !string.IsNullOrWhiteSpace(line)
                && pending.Description.Length > 0)
            {
                pending.Description += " " + line;
            }
        }

        if (pending is not null)
            results.Add(Build(pending, accountIdentifier, config));

        return results;
    }

    private static RawImportedTransaction Build(Mt940Statement statement, string? account, Mt940ImporterConfig config) =>
        new()
        {
            Amount = statement.Amount,
            CurrencyCode = config.DefaultCurrency.ToUpperInvariant(),
            TransactionDate = statement.Date,
            Description = statement.Description,
            ReferenceDoc = string.IsNullOrWhiteSpace(statement.Reference) ? null : statement.Reference,
            AccountIdentifier = account,
            OriginalPayloadJson = JsonSerializer.Serialize(statement)
        };

    private static Mt940Statement? ParseStatementLine(string body)
    {
        if (body.Length < 7)
            return null;

        var valueDateRaw = body[..6];
        if (!DateTime.TryParseExact(valueDateRaw, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;

        var cursor = 6;

        if (cursor + 4 <= body.Length && body.Skip(cursor).Take(4).All(char.IsDigit))
            cursor += 4;

        var isReversal = cursor < body.Length && body[cursor] == 'R';
        if (isReversal) cursor++;

        if (cursor >= body.Length)
            return null;

        var marker = body[cursor];
        if (marker is not ('D' or 'C'))
            return null;
        cursor++;

        var amountStart = cursor;
        while (cursor < body.Length && (char.IsDigit(body[cursor]) || body[cursor] == ','))
            cursor++;

        var amountRaw = body[amountStart..cursor].Replace(',', '.');
        if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return null;

        var signedAmount = marker == 'D' ? -amount : amount;
        if (isReversal) signedAmount = -signedAmount;

        var remainder = cursor < body.Length ? body[cursor..] : string.Empty;
        var reference = string.Empty;
        var separatorIndex = remainder.IndexOf("//", StringComparison.Ordinal);
        if (separatorIndex >= 0)
            reference = remainder[(separatorIndex + 2)..].Trim();

        return new Mt940Statement
        {
            Date = date,
            Amount = signedAmount,
            Reference = reference,
            Description = string.Empty
        };
    }

    private class Mt940Statement
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

public class Mt940ImporterConfig
{
    public string DefaultCurrency { get; set; } = "BRL";
}
