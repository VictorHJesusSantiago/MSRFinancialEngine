using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

public interface ISourceImporter
{
    SourceType SupportedType { get; }

    IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson);
}

public interface ISourceImporterFactory
{
    ISourceImporter GetImporter(SourceType type);
}
