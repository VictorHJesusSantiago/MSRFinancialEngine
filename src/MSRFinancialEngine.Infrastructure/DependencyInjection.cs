using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Application.Reports;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' não configurada.");

        services.AddDbContext<FinancialEngineDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ISourceImporter, CsvBankStatementImporter>();
        services.AddScoped<ISourceImporter, OfxBankStatementImporter>();
        services.AddScoped<ISourceImporter, ErpJsonImporter>();
        services.AddScoped<ISourceImporterFactory, SourceImporterFactory>();
        services.AddScoped<IImportService, ImportService>();

        services.AddScoped<IMatchingStrategy, DeterministicMatchingStrategy>();
        services.AddScoped<IMatchingStrategy, FuzzyMatchingStrategy>();
        services.AddScoped<IMatchingEngine, MatchingEngine>();

        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
