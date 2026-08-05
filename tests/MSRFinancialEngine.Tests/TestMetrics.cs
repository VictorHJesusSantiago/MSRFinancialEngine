using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using MSRFinancialEngine.Application.Observability;

namespace MSRFinancialEngine.Tests;

public static class TestMetrics
{
    public static EngineMetrics Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        return new EngineMetrics(services.BuildServiceProvider().GetRequiredService<IMeterFactory>());
    }
}
