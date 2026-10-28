using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VisitorManagement.Infrastructure;
using VisitorManagement.Infrastructure.Messaging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Config-ready: telemetry flows only when APPLICATIONINSIGHTS_CONNECTION_STRING is set.
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Let Information-level logs reach Application Insights (the default filter drops them).
        services.Configure<LoggerFilterOptions>(options =>
        {
            var rule = options.Rules.FirstOrDefault(r =>
                r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (rule is not null) options.Rules.Remove(rule);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(context.Configuration);   // DbContext
        services.AddScoped<VisitorCreatedAuditProcessor>();
    })
    .Build();

host.Run();