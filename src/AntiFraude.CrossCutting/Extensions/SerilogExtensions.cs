using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace AntiFraude.CrossCutting.Extensions;

public static class SerilogExtensions
{
    /// <summary>
    /// Configura Serilog com structured logging e enrichers padrão.
    /// Todos os logs conterão: CorrelationId (do LogContext), MachineName, ThreadId.
    /// TransactionId deve ser adicionado via LogContext.PushProperty("TransactionId", ...)
    /// nos pontos de processamento de transação.
    /// </summary>
    public static IHostBuilder UseSerilogStructuredLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("MassTransit", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{TransactionId}] {Message:lj}{NewLine}{Exception}");
        });
    }

    public static IApplicationBuilder UseCorrelationIdLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(opts =>
        {
            opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
        });
        return app;
    }
}
