namespace MSRFinancialEngine.Application.Audit;

public interface IAuditService
{
    Task LogAsync(string entityType, Guid entityId, string action, Guid? userId, object? details = null, CancellationToken ct = default);
}
