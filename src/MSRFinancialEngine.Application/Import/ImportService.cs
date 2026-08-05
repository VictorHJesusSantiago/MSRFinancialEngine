using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Observability;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Import;

public interface IImportService
{
    Task<ImportResult> ImportAsync(Guid sourceId, Stream content, CancellationToken ct = default);
}

public class ImportResult
{
    public int TotalParsed { get; set; }
    public int Imported { get; set; }
    public int Duplicates { get; set; }
}

public class ImportService : IImportService
{
    private readonly ISourceImporterFactory _importerFactory;
    private readonly IRepository<Source> _sourceRepository;
    private readonly IRepository<RawTransaction> _rawTransactionRepository;
    private readonly IRepository<CanonicalTransaction> _canonicalTransactionRepository;
    private readonly IAuditService _auditService;
    private readonly EngineMetrics _metrics;
    private readonly IUnitOfWork _unitOfWork;

    public ImportService(
        ISourceImporterFactory importerFactory,
        IRepository<Source> sourceRepository,
        IRepository<RawTransaction> rawTransactionRepository,
        IRepository<CanonicalTransaction> canonicalTransactionRepository,
        IAuditService auditService,
        EngineMetrics metrics,
        IUnitOfWork unitOfWork)
    {
        _importerFactory = importerFactory;
        _sourceRepository = sourceRepository;
        _rawTransactionRepository = rawTransactionRepository;
        _canonicalTransactionRepository = canonicalTransactionRepository;
        _auditService = auditService;
        _metrics = metrics;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> ImportAsync(Guid sourceId, Stream content, CancellationToken ct = default)
    {
        var source = await _sourceRepository.GetByIdAsync(sourceId, ct)
            ?? throw new InvalidOperationException($"Fonte '{sourceId}' não encontrada.");

        if (!source.Active)
            throw new InvalidOperationException(
                $"A fonte '{source.Name}' está desativada e não aceita novas importações.");

        var importer = _importerFactory.GetImporter(source.Type);
        var parsed = importer.Parse(content, source.ConfigJson);

        var result = new ImportResult { TotalParsed = parsed.Count };
        var existingHashes = _canonicalTransactionRepository.Query()
            .Where(t => t.CompanyId == source.CompanyId)
            .Select(t => t.Hash)
            .ToHashSet();

        foreach (var item in parsed)
        {
            var hash = ComputeHash(source.CompanyId, item);

            if (existingHashes.Contains(hash))
            {
                result.Duplicates++;
                continue;
            }

            var raw = new RawTransaction
            {
                SourceId = source.Id,
                PayloadJson = item.OriginalPayloadJson,
                Normalized = true
            };
            await _rawTransactionRepository.AddAsync(raw, ct);

            var canonical = new CanonicalTransaction
            {
                CompanyId = source.CompanyId,
                SourceId = source.Id,
                RawTransactionId = raw.Id,
                Amount = item.Amount,
                CurrencyCode = item.CurrencyCode,
                TransactionDate = item.TransactionDate,
                Description = NormalizeDescription(item.Description),
                ReferenceDoc = item.ReferenceDoc,
                AccountIdentifier = item.AccountIdentifier,
                Hash = hash
            };
            await _canonicalTransactionRepository.AddAsync(canonical, ct);

            existingHashes.Add(hash);
            result.Imported++;
        }

        await _auditService.LogAsync(nameof(Source), source.Id, "Import", null,
            new { result.TotalParsed, result.Imported, result.Duplicates }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _metrics.TransactionsImported(result.Imported, source.CompanyId);
        return result;
    }

    private static string NormalizeDescription(string description) =>
        string.Join(' ', description.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string ComputeHash(Guid companyId, RawImportedTransaction item)
    {
        var raw = $"{companyId}|{item.Amount}|{item.CurrencyCode}|{item.TransactionDate:yyyy-MM-dd}|{item.ReferenceDoc}|{item.AccountIdentifier}|{item.Description.Trim().ToUpperInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
