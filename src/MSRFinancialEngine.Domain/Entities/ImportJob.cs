namespace MSRFinancialEngine.Domain.Entities;

public enum ImportJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }
    public Source? Source { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StagedFilePath { get; set; } = string.Empty;

    public ImportJobStatus Status { get; set; } = ImportJobStatus.Queued;

    public int TotalParsed { get; set; }
    public int Imported { get; set; }
    public int Duplicates { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}
