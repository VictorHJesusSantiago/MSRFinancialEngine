using Microsoft.Extensions.Configuration;
using MSRFinancialEngine.Application.Audit;

namespace MSRFinancialEngine.Infrastructure.Audit;

public class FileSystemAuditArchiveStore : IAuditArchiveStore
{
    private readonly string _root;

    public FileSystemAuditArchiveStore(IConfiguration configuration)
    {
        _root = configuration["Retention:AuditArchivePath"]
                ?? Path.Combine(Path.GetTempPath(), "msr-financial-engine", "audit-archive");

        Directory.CreateDirectory(_root);
    }

    public async Task<string> WriteAsync(string name, Stream content, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, name);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);

        return path;
    }

    public Task<Stream?> OpenReadAsync(string location, CancellationToken ct = default)
    {
        Stream? stream = File.Exists(location) ? File.OpenRead(location) : null;
        return Task.FromResult(stream);
    }
}
