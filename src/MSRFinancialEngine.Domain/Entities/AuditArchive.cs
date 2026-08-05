namespace MSRFinancialEngine.Domain.Entities;

public class AuditArchive
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public int EventCount { get; set; }

    public string Location { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}
