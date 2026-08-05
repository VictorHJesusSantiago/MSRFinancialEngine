using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Import;

public interface IReprocessService
{
    Task<ReprocessResult> InvalidateSourceAsync(Guid sourceId, CancellationToken ct = default);
}

public class ReprocessResult
{
    public int CanonicalRemoved { get; set; }
    public int RawMarkedForReimport { get; set; }
    public int PreservedBecauseReconciled { get; set; }
}

public class ReprocessService : IReprocessService
{
    private readonly IRepository<Source> _sourceRepository;
    private readonly IRepository<CanonicalTransaction> _canonicalRepository;
    private readonly IRepository<RawTransaction> _rawRepository;
    private readonly IRepository<Divergence> _divergenceRepository;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public ReprocessService(
        IRepository<Source> sourceRepository,
        IRepository<CanonicalTransaction> canonicalRepository,
        IRepository<RawTransaction> rawRepository,
        IRepository<Divergence> divergenceRepository,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _sourceRepository = sourceRepository;
        _canonicalRepository = canonicalRepository;
        _rawRepository = rawRepository;
        _divergenceRepository = divergenceRepository;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReprocessResult> InvalidateSourceAsync(Guid sourceId, CancellationToken ct = default)
    {
        var source = await _sourceRepository.GetByIdAsync(sourceId, ct)
            ?? throw new InvalidOperationException($"Fonte '{sourceId}' não encontrada.");

        var transactions = _canonicalRepository.Query()
            .Where(t => t.SourceId == source.Id)
            .ToList();

        var result = new ReprocessResult
        {
            PreservedBecauseReconciled = transactions.Count(t => t.Reconciled)
        };

        var removable = transactions.Where(t => !t.Reconciled).ToList();
        var removableIds = removable.Select(t => t.Id).ToHashSet();

        var divergences = _divergenceRepository.Query()
            .Where(d => removableIds.Contains(d.TransactionId))
            .ToList();

        foreach (var divergence in divergences)
            _divergenceRepository.Remove(divergence);

        var rawIds = removable
            .Where(t => t.RawTransactionId.HasValue)
            .Select(t => t.RawTransactionId!.Value)
            .ToHashSet();

        foreach (var transaction in removable)
            _canonicalRepository.Remove(transaction);

        result.CanonicalRemoved = removable.Count;

        var rawTransactions = _rawRepository.Query()
            .Where(r => rawIds.Contains(r.Id))
            .ToList();

        foreach (var raw in rawTransactions)
        {
            raw.Normalized = false;
            _rawRepository.Update(raw);
        }

        result.RawMarkedForReimport = rawTransactions.Count;

        await _auditService.LogAsync(nameof(Source), source.Id, "InvalidatedForReprocessing", null,
            new
            {
                result.CanonicalRemoved,
                result.RawMarkedForReimport,
                result.PreservedBecauseReconciled
            }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }
}
