using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Tests.Matching;

public class MatchingConcurrencyTests
{
    [Fact]
    public async Task Guard_blocks_a_second_run_for_the_same_company()
    {
        await using var context = TestDbContextFactory.Create();
        var guard = new PostgresMatchingRunGuard(context);
        var companyId = Guid.NewGuid();

        await using var first = await guard.TryAcquireAsync(companyId);
        Assert.NotNull(first);

        var second = await guard.TryAcquireAsync(companyId);
        Assert.Null(second);
    }

    [Fact]
    public async Task Different_companies_run_independently()
    {
        await using var context = TestDbContextFactory.Create();
        var guard = new PostgresMatchingRunGuard(context);

        await using var companyA = await guard.TryAcquireAsync(Guid.NewGuid());
        await using var companyB = await guard.TryAcquireAsync(Guid.NewGuid());

        Assert.NotNull(companyA);
        Assert.NotNull(companyB);
    }

    [Fact]
    public async Task Lock_is_released_when_the_run_finishes()
    {
        await using var context = TestDbContextFactory.Create();
        var guard = new PostgresMatchingRunGuard(context);
        var companyId = Guid.NewGuid();

        var first = await guard.TryAcquireAsync(companyId);
        Assert.NotNull(first);
        await first!.DisposeAsync();

        await using var afterRelease = await guard.TryAcquireAsync(companyId);
        Assert.NotNull(afterRelease);
    }

    [Fact]
    public async Task Engine_refuses_to_start_while_another_run_holds_the_company()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Concorrência", BaseCurrencyCode = "BRL" };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var guard = new PostgresMatchingRunGuard(context);

        await using var running = await guard.TryAcquireAsync(company.Id);
        Assert.NotNull(running);

        await Assert.ThrowsAsync<MatchingAlreadyRunningException>(() =>
            MatchingEngineFactory.Build(context).RunForCompanyAsync(company.Id));
    }

    [Fact]
    public async Task Sequential_runs_do_not_reconcile_the_same_pair_twice()
    {
        await using var context = TestDbContextFactory.Create();

        var company = new Company { Name = "Sequencial", BaseCurrencyCode = "BRL" };
        var bank = new Source { CompanyId = company.Id, Name = "Banco", Type = SourceType.BankStatementCsv };
        var erp = new Source { CompanyId = company.Id, Name = "ERP", Type = SourceType.ErpJson };
        context.Companies.Add(company);
        context.Sources.AddRange(bank, erp);
        context.MatchingRules.Add(new MatchingRule
        {
            CompanyId = company.Id,
            Name = "Det",
            Type = MatchingRuleType.Deterministic,
            ConfigJson = "{\"toleranceAmount\":0,\"toleranceDays\":1}",
            Priority = 1
        });
        context.CanonicalTransactions.AddRange(
            new CanonicalTransaction
            {
                CompanyId = company.Id, SourceId = bank.Id, Amount = 100m, CurrencyCode = "BRL",
                TransactionDate = new DateTime(2026, 1, 10), Description = "PAG", ReferenceDoc = "NF-1", Hash = "h1"
            },
            new CanonicalTransaction
            {
                CompanyId = company.Id, SourceId = erp.Id, Amount = 100m, CurrencyCode = "BRL",
                TransactionDate = new DateTime(2026, 1, 10), Description = "PAG", ReferenceDoc = "NF-1", Hash = "h2"
            });
        await context.SaveChangesAsync();

        var engine = MatchingEngineFactory.Build(context);

        var first = await engine.RunForCompanyAsync(company.Id);
        var second = await engine.RunForCompanyAsync(company.Id);

        Assert.Equal(1, first.AutoApproved);

        Assert.Equal(0, second.TransactionsConsidered);
        Assert.Equal(0, second.AutoApproved);
        Assert.Single(context.MatchCandidates);
    }
}
