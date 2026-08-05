using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Matching;

public class MatchingEngineTests
{
    [Fact]
    public async Task Auto_approves_deterministic_match_and_creates_divergence_for_unmatched()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Empresa Teste", BaseCurrencyCode = "BRL" };
        var sourceBank = new Source { CompanyId = company.Id, Name = "Banco", Type = SourceType.BankStatementCsv };
        var sourceErp = new Source { CompanyId = company.Id, Name = "ERP", Type = SourceType.ErpJson };
        context.Companies.Add(company);
        context.Sources.AddRange(sourceBank, sourceErp);

        var rule = new MatchingRule
        {
            CompanyId = company.Id,
            Name = "Determinístico padrão",
            Type = MatchingRuleType.Deterministic,
            ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":1}",
            Priority = 1,
            Active = true
        };
        context.MatchingRules.Add(rule);

        var matched1 = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = sourceBank.Id, Amount = 100m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-001", Hash = "h1"
        };
        var matched2 = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = sourceErp.Id, Amount = 100m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "PAGAMENTO", ReferenceDoc = "NF-001", Hash = "h2"
        };
        var orphan = new CanonicalTransaction
        {
            CompanyId = company.Id, SourceId = sourceBank.Id, Amount = 999m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 15), Description = "SEM PAR", Hash = "h3"
        };
        context.CanonicalTransactions.AddRange(matched1, matched2, orphan);
        await context.SaveChangesAsync();

        var engine = MatchingEngineFactory.Build(context);

        var result = await engine.RunForCompanyAsync(company.Id);

        Assert.Equal(3, result.TransactionsConsidered);
        Assert.Equal(1, result.AutoApproved);
        Assert.Equal(1, result.DivergencesCreated);

        await context.Entry(matched1).ReloadAsync();
        await context.Entry(matched2).ReloadAsync();
        await context.Entry(orphan).ReloadAsync();

        Assert.True(matched1.Reconciled);
        Assert.True(matched2.Reconciled);
        Assert.False(orphan.Reconciled);

        var divergence = context.Divergences.Single();
        Assert.Equal(orphan.Id, divergence.TransactionId);
        Assert.Equal(DivergenceReason.NoCandidate, divergence.Reason);
    }
}
