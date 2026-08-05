using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MSRFinancialEngine.Application.Abstractions;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Infrastructure.Auth;
using MSRFinancialEngine.Application.Audit;
using MSRFinancialEngine.Application.Currency;
using MSRFinancialEngine.Application.Import;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Application.Observability;
using MSRFinancialEngine.Application.Reports;
using MSRFinancialEngine.Application.Retention;
using MSRFinancialEngine.Infrastructure.Retention;
using MSRFinancialEngine.Application.Workflow;
using MSRFinancialEngine.Infrastructure.Audit;
using MSRFinancialEngine.Infrastructure.Import;
using MSRFinancialEngine.Infrastructure.Persistence;

namespace MSRFinancialEngine.Infrastructure;

public static class DependencyInjection
{
    private const string AuthOptionsSection = "Auth";

    public static IServiceCollection AddFinancialEngineInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' não configurada.");

        services.AddDbContext<FinancialEngineDbContext>(options => options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));

        services.AddMetrics();
        services.AddSingleton<EngineMetrics>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton(configuration.GetSection(AuthOptionsSection).Get<AuthOptions>() ?? new AuthOptions());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseSeeder>();

        services.AddScoped<ICompanyContext, CompanyContext>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ISourceImporter, CsvBankStatementImporter>();
        services.AddScoped<ISourceImporter, OfxBankStatementImporter>();
        services.AddScoped<ISourceImporter, ErpJsonImporter>();
        services.AddScoped<ISourceImporter, NfeXmlImporter>();
        services.AddScoped<ISourceImporter, Mt940BankStatementImporter>();
        services.AddScoped<ISourceImporterFactory, SourceImporterFactory>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IReprocessService, ReprocessService>();
        services.AddSingleton<IImportJobSignal, ImportJobSignal>();
        services.AddScoped<IImportJobClaimer, PostgresImportJobClaimer>();
        services.AddSingleton(configuration.GetSection(ImportJobWorkerOptions.SectionName)
            .Get<ImportJobWorkerOptions>() ?? new ImportJobWorkerOptions());
        services.AddSingleton<IImportStagingStore, FileSystemImportStagingStore>();
        services.AddScoped<IImportJobService, ImportJobService>();
        services.AddHostedService<ImportJobWorker>();

        services.AddSingleton(configuration.GetSection(RetentionOptions.SectionName)
            .Get<RetentionOptions>() ?? new RetentionOptions());
        services.AddSingleton<IAuditArchiveStore, FileSystemAuditArchiveStore>();
        services.AddScoped<IAuditArchiveService, AuditArchiveService>();
        services.AddScoped<IRetentionService, RetentionService>();
        services.AddHostedService<RetentionWorker>();

        services.AddScoped<IMatchingStrategy, DeterministicMatchingStrategy>();
        services.AddScoped<IMatchingStrategy, FuzzyMatchingStrategy>();
        services.AddScoped<IMatchingRunGuard, PostgresMatchingRunGuard>();
        services.AddScoped<IMatchingEngine, MatchingEngine>();

        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
