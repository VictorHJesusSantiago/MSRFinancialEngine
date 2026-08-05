using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Audit;

public interface IAuditArchiveStore
{
    Task<string> WriteAsync(string name, Stream content, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string location, CancellationToken ct = default);
}

public interface IAuditArchiveService
{
    Task<AuditArchive> ArchiveAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken ct = default);

    Task<bool> VerifyAsync(Guid archiveId, CancellationToken ct = default);

    Task<AuditArchive?> ArchivePendingAsync(int lagDays, CancellationToken ct = default);
}

public class AuditArchiveService : IAuditArchiveService
{
    private readonly IRepository<AuditEvent> _auditEvents;
    private readonly IRepository<AuditArchive> _archives;
    private readonly IAuditArchiveStore _store;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public AuditArchiveService(
        IRepository<AuditEvent> auditEvents,
        IRepository<AuditArchive> archives,
        IAuditArchiveStore store,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _auditEvents = auditEvents;
        _archives = archives;
        _store = store;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuditArchive> ArchiveAsync(DateTime fromUtc, DateTime toUtc, Guid? userId, CancellationToken ct = default)
    {
        var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);

        if (to <= from)
            throw new InvalidOperationException("O fim do período deve ser posterior ao início.");

        if (to > DateTime.UtcNow.AddMinutes(-1))
            throw new InvalidOperationException("Só é possível arquivar períodos já encerrados.");

        var events = _auditEvents.Query()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to && e.ArchivedAtUtc == null)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (events.Count == 0)
            throw new InvalidOperationException("Não há eventos de auditoria no período informado.");

        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var builder = new StringBuilder();
        foreach (var e in events)
        {
            builder.AppendLine(JsonSerializer.Serialize(new
            {
                e.Id,
                e.EntityType,
                e.EntityId,
                e.Action,
                e.UserId,
                e.Timestamp,
                e.DetailsJson
            }, json));
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var name = $"audit-{from:yyyyMMddHHmmss}-{to:yyyyMMddHHmmss}.jsonl";

        var location = await _store.WriteAsync(name, new MemoryStream(bytes), ct);

        var archive = new AuditArchive
        {
            FromUtc = from,
            ToUtc = to,
            EventCount = events.Count,
            Location = location,
            ContentHash = hash,
            CreatedByUserId = userId
        };

        foreach (var e in events)
        {
            e.ArchivedAtUtc = archive.CreatedAtUtc;
            _auditEvents.Update(e);
        }

        await _archives.AddAsync(archive, ct);
        await _auditService.LogAsync(nameof(AuditArchive), archive.Id, "Archived", userId,
            new { archive.FromUtc, archive.ToUtc, archive.EventCount, archive.Location }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return archive;
    }

    public async Task<AuditArchive?> ArchivePendingAsync(int lagDays, CancellationToken ct = default)
    {
        var to = DateTime.UtcNow.AddDays(-Math.Max(lagDays, 0));

        var from = _auditEvents.Query()
            .Where(e => e.ArchivedAtUtc == null && e.Timestamp <= to)
            .OrderBy(e => e.Timestamp)
            .Select(e => (DateTime?)e.Timestamp)
            .FirstOrDefault();

        if (from is null)
            return null;

        return await ArchiveAsync(from.Value, to, null, ct);
    }

    public async Task<bool> VerifyAsync(Guid archiveId, CancellationToken ct = default)
    {
        var archive = await _archives.GetByIdAsync(archiveId, ct)
            ?? throw new InvalidOperationException($"Arquivo de auditoria '{archiveId}' não encontrado.");

        await using var content = await _store.OpenReadAsync(archive.Location, ct);
        if (content is null)
            return false;

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        return string.Equals(hash, archive.ContentHash, StringComparison.OrdinalIgnoreCase);
    }
}
