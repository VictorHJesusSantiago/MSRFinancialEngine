using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public class SourceImporterFactory : ISourceImporterFactory
{
    private readonly Dictionary<SourceType, ISourceImporter> _importers;

    public SourceImporterFactory(IEnumerable<ISourceImporter> importers)
    {
        _importers = importers.ToDictionary(i => i.SupportedType);
    }

    public ISourceImporter GetImporter(SourceType type)
    {
        if (!_importers.TryGetValue(type, out var importer))
            throw new NotSupportedException($"Nenhum importador registrado para o tipo de fonte '{type}'.");

        return importer;
    }
}
