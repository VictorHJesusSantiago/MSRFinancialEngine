using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Matching;

public static class MatchingEngineFactory
{
    public static MatchingEngine Build(FinancialEngineDbContext context) => new(
        new EfRepository<CanonicalTransaction>(context),
        new EfRepository<MatchingRule>(context),
        new EfRepository<MatchCandidate>(context),
        new EfRepository<Divergence>(context),
        new EfRepository<Company>(context),
        new IMatchingStrategy[] { new DeterministicMatchingStrategy(), new FuzzyMatchingStrategy() },
        new CurrencyConversionService(new EfRepository<ExchangeRate>(context)),
        new PostgresMatchingRunGuard(context),
        TestMetrics.Create(),
        new AuditService(new EfRepository<AuditEvent>(context)),
        new EfUnitOfWork(context));
}
