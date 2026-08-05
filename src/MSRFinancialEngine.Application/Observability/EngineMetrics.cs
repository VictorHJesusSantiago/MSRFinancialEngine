using System.Diagnostics.Metrics;

namespace MSRFinancialEngine.Application.Observability;

public class EngineMetrics
{
    public const string MeterName = "MSRFinancialEngine";

    private readonly Counter<long> _transactionsImported;
    private readonly Counter<long> _transactionsAutoReconciled;
    private readonly Counter<long> _divergencesCreated;
    private readonly Counter<long> _decisionsRecorded;
    private readonly Counter<long> _missingExchangeRates;
    private readonly Histogram<double> _matchingDurationMs;

    public EngineMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _transactionsImported = meter.CreateCounter<long>(
            "msr.transactions.imported", "transações", "Transações normalizadas e persistidas.");
        _transactionsAutoReconciled = meter.CreateCounter<long>(
            "msr.transactions.auto_reconciled", "transações", "Pares conciliados automaticamente pelo motor.");
        _divergencesCreated = meter.CreateCounter<long>(
            "msr.divergences.created", "divergências", "Divergências abertas para revisão humana.");
        _decisionsRecorded = meter.CreateCounter<long>(
            "msr.decisions.recorded", "decisões", "Decisões manuais registradas sobre divergências.");
        _missingExchangeRates = meter.CreateCounter<long>(
            "msr.exchange_rates.missing", "transações", "Transações sem taxa de câmbio para a data.");
        _matchingDurationMs = meter.CreateHistogram<double>(
            "msr.matching.duration", "ms", "Duração de uma execução do motor de matching.");
    }

    public void TransactionsImported(long count, Guid companyId) =>
        _transactionsImported.Add(count, Tag(companyId));

    public void MatchingCompleted(Guid companyId, int autoReconciled, int divergences, int missingRates, double elapsedMs)
    {
        var tag = Tag(companyId);

        _transactionsAutoReconciled.Add(autoReconciled, tag);
        _divergencesCreated.Add(divergences, tag);
        _missingExchangeRates.Add(missingRates, tag);
        _matchingDurationMs.Record(elapsedMs, tag);
    }

    public void DecisionRecorded(string decision) =>
        _decisionsRecorded.Add(1, new KeyValuePair<string, object?>("decision", decision));

    private static KeyValuePair<string, object?> Tag(Guid companyId) => new("company_id", companyId.ToString());
}
