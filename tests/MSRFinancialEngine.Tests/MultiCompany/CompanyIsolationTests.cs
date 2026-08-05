using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.MultiCompany;

public class CompanyIsolationTests
{
    private static (Guid companyA, Guid companyB, string database) Seed()
    {
        var database = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        using var context = TestDbContextFactory.CreateForCompany(database, null);

        var sourceA = new Source { CompanyId = companyA, Name = "Fonte A", Type = SourceType.BankStatementCsv };
        var sourceB = new Source { CompanyId = companyB, Name = "Fonte B", Type = SourceType.ErpJson };
        context.Sources.AddRange(sourceA, sourceB);

        var txA = new CanonicalTransaction
        {
            CompanyId = companyA, SourceId = sourceA.Id, Amount = 100m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "EMPRESA A", Hash = "ha"
        };
        var txB = new CanonicalTransaction
        {
            CompanyId = companyB, SourceId = sourceB.Id, Amount = 200m, CurrencyCode = "BRL",
            TransactionDate = new DateTime(2026, 1, 10), Description = "EMPRESA B", Hash = "hb"
        };
        context.CanonicalTransactions.AddRange(txA, txB);

        context.MatchingRules.AddRange(
            new MatchingRule { CompanyId = companyA, Name = "Regra A", Type = MatchingRuleType.Deterministic },
            new MatchingRule { CompanyId = companyB, Name = "Regra B", Type = MatchingRuleType.Deterministic });

        context.Divergences.AddRange(
            new Divergence { TransactionId = txA.Id, Reason = DivergenceReason.NoCandidate },
            new Divergence { TransactionId = txB.Id, Reason = DivergenceReason.NoCandidate });

        context.RawTransactions.AddRange(
            new RawTransaction { SourceId = sourceA.Id, PayloadJson = "{}" },
            new RawTransaction { SourceId = sourceB.Id, PayloadJson = "{}" });

        context.SaveChanges();
        return (companyA, companyB, database);
    }

    [Fact]
    public void Transactions_are_scoped_to_the_active_company()
    {
        var (companyA, _, database) = Seed();

        using var context = TestDbContextFactory.CreateForCompany(database, companyA);
        var transactions = context.CanonicalTransactions.ToList();

        Assert.Single(transactions);
        Assert.Equal("EMPRESA A", transactions[0].Description);
    }

    [Fact]
    public void Sources_matching_rules_and_divergences_are_scoped_to_the_active_company()
    {
        var (_, companyB, database) = Seed();

        using var context = TestDbContextFactory.CreateForCompany(database, companyB);

        Assert.Equal("Fonte B", context.Sources.Single().Name);
        Assert.Equal("Regra B", context.MatchingRules.Single().Name);
        Assert.Single(context.Divergences.ToList());
        Assert.Single(context.RawTransactions.ToList());
    }

    [Fact]
    public void Without_an_active_company_no_filter_is_applied()
    {
        var (_, _, database) = Seed();

        using var context = TestDbContextFactory.CreateForCompany(database, null);

        Assert.Equal(2, context.CanonicalTransactions.Count());
        Assert.Equal(2, context.Sources.Count());
        Assert.Equal(2, context.MatchingRules.Count());
    }

    [Fact]
    public void Transaction_from_another_company_is_not_reachable_by_id()
    {
        var (companyA, companyB, database) = Seed();

        Guid otherCompanyTransactionId;
        using (var seeded = TestDbContextFactory.CreateForCompany(database, companyB))
            otherCompanyTransactionId = seeded.CanonicalTransactions.Single().Id;

        using var context = TestDbContextFactory.CreateForCompany(database, companyA);
        var found = context.CanonicalTransactions.FirstOrDefault(t => t.Id == otherCompanyTransactionId);

        Assert.Null(found);
    }

    [Fact]
    public void IgnoreQueryFilters_allows_deliberate_cross_company_access()
    {
        var (companyA, _, database) = Seed();

        using var context = TestDbContextFactory.CreateForCompany(database, companyA);
        var all = context.CanonicalTransactions.IgnoreQueryFilters().ToList();

        Assert.Equal(2, all.Count);
    }
}
