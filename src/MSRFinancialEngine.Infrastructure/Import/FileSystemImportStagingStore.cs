using Microsoft.Extensions.Logging;
using MSRFinancialEngine.Application.Import;

namespace MSRFinancialEngine.Infrastructure.Import;

public class FileSystemImportStagingStore : IImportStagingStore
{
    private readonly string _root;
    private readonly ILogger<FileSystemImportStagingStore> _logger;

    public FileSystemImportStagingStore(ILogger<FileSystemImportStagingStore> logger)
    {
        _logger = logger;
        _root = Path.Combine(Path.GetTempPath(), "msr-financial-engine", "import-staging");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> StageAsync(Guid jobId, Stream content, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, $"{jobId:N}.staged");

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);

        return path;
    }

    public Stream OpenRead(string path) => File.OpenRead(path);

    public void Discard(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Não foi possível remover o arquivo temporário {Path}", path);
        }
    }
}
