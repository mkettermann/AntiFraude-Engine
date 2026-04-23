using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AntiFraude.CrossCutting.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter()) // compatível com Jaeger/Tempo via env OTEL_EXPORTER_OTLP_ENDPOINT
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()); // expõe /metrics

        return services;
    }
}
