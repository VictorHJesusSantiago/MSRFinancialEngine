namespace MSRFinancialEngine.Domain.Entities;

/// <summary>Registro imutável de auditoria para qualquer ação relevante do sistema.</summary>
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DetailsJson { get; set; } = "{}";
}
