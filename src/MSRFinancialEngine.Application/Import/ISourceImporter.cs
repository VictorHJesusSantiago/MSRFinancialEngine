using MSRFinancialEngine.Domain;

namespace MSRFinancialEngine.Application.Import;

/// <summary>
/// Contrato que toda fonte de importação deve implementar. Adicionar suporte a um novo
/// formato/fonte = criar uma nova implementação e registrá-la em <see cref="ISourceImporterFactory"/>,
/// sem tocar no núcleo de normalização ou matching.
/// </summary>
public interface ISourceImporter
{
    SourceType SupportedType { get; }

    /// <summary>Lê o conteúdo bruto da fonte e produz transações já parseadas (mas não normalizadas).</summary>
    IReadOnlyList<RawImportedTransaction> Parse(Stream content, string configJson);
}

public interface ISourceImporterFactory
{
    ISourceImporter GetImporter(SourceType type);
}
