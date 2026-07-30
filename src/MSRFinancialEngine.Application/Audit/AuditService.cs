using System.Text.Json;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Application.Audit;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditEvent> _auditRepository;

    public AuditService(IRepository<AuditEvent> auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task LogAsync(string entityType, Guid entityId, string action, Guid? userId, object? details = null, CancellationToken ct = default)
    {
        var evt = new AuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            DetailsJson = details is null ? "{}" : JsonSerializer.Serialize(details)
        };

        await _auditRepository.AddAsync(evt, ct);
    }
}
