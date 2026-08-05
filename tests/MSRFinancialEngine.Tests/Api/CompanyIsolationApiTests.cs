using System.Net;
using System.Net.Http.Json;
using MSRFinancialEngine.Domain;
using MSRFinancialEngine.Domain.Entities;

namespace MSRFinancialEngine.Tests.Api;

public class CompanyIsolationApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();

    public CompanyIsolationApiTests(ApiTestFactory factory)
    {
        _factory = factory;

        _factory.SeedDatabase((context, _) =>
        {
            if (context.Companies.Any(c => c.Id == _companyA))
                return;

            context.Companies.AddRange(
                new Company { Id = _companyA, Name = "Empresa A", BaseCurrencyCode = "BRL" },
                new Company { Id = _companyB, Name = "Empresa B", BaseCurrencyCode = "BRL" });

            var sourceA = new Source { CompanyId = _companyA, Name = "Fonte A", Type = SourceType.BankStatementCsv };
            var sourceB = new Source { CompanyId = _companyB, Name = "Fonte B", Type = SourceType.ErpJson };
            context.Sources.AddRange(sourceA, sourceB);

            context.CanonicalTransactions.AddRange(
                new CanonicalTransaction
                {
                    CompanyId = _companyA, SourceId = sourceA.Id, Amount = 100m, CurrencyCode = "BRL",
                    TransactionDate = new DateTime(2026, 1, 10), Description = "SEGREDO DA EMPRESA A", Hash = "ha"
                },
                new CanonicalTransaction
                {
                    CompanyId = _companyB, SourceId = sourceB.Id, Amount = 200m, CurrencyCode = "BRL",
                    TransactionDate = new DateTime(2026, 1, 10), Description = "SEGREDO DA EMPRESA B", Hash = "hb"
                });
        });
    }

    [Fact]
    public async Task User_only_sees_transactions_of_the_company_in_their_token()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345", companyId: _companyA);
        var client = await _factory.CreateAuthenticatedClientAsync(user, "Senha@12345");

        var page = await client.GetFromJsonAsync<PagedResponse<TransactionDto>>(
            $"/api/transactions?companyId={_companyA}");

        Assert.Single(page!.Items);
        Assert.Equal("SEGREDO DA EMPRESA A", page.Items[0].Description);
    }

    [Fact]
    public async Task Querying_another_company_by_id_returns_nothing()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345", companyId: _companyA);
        var client = await _factory.CreateAuthenticatedClientAsync(user, "Senha@12345");

        var page = await client.GetFromJsonAsync<PagedResponse<TransactionDto>>(
            $"/api/transactions?companyId={_companyB}");

        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Company_header_is_ignored_for_non_admin_users()
    {
        var user = _factory.AddUser(UserRole.Approver, "Senha@12345", companyId: _companyA);
        var client = await _factory.CreateAuthenticatedClientAsync(user, "Senha@12345");
        client.DefaultRequestHeaders.Add("X-Company-Id", _companyB.ToString());

        var page = await client.GetFromJsonAsync<PagedResponse<TransactionDto>>(
            $"/api/transactions?companyId={_companyB}");

        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Corporate_admin_can_scope_to_a_company_through_the_header()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345", companyId: null);
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");
        client.DefaultRequestHeaders.Add("X-Company-Id", _companyB.ToString());

        var page = await client.GetFromJsonAsync<PagedResponse<TransactionDto>>(
            $"/api/transactions?companyId={_companyB}");

        Assert.Single(page!.Items);
        Assert.Equal("SEGREDO DA EMPRESA B", page.Items[0].Description);
    }

    [Fact]
    public async Task Listing_is_paginated_and_page_size_is_capped()
    {
        var admin = _factory.AddUser(UserRole.Admin, "Senha@12345");
        var client = await _factory.CreateAuthenticatedClientAsync(admin, "Senha@12345");

        var page = await client.GetFromJsonAsync<PagedResponse<CompanyDto>>("/api/companies?page=1&pageSize=99999");

        Assert.Equal(200, page!.PageSize);
        Assert.True(page.TotalItems >= 2);
    }

    private record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
    private record TransactionDto(Guid Id, string Description, decimal Amount);
    private record CompanyDto(Guid Id, string Name);
}
