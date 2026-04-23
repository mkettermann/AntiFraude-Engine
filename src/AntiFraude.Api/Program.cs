using AntiFraude.Api.Endpoints;
using AntiFraude.CrossCutting.Extensions;
using AntiFraude.CrossCutting.Middleware;
using AntiFraude.Infrastructure.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog — structured logging ───────────────────────────────────────────────
builder.Host.UseSerilogStructuredLogging();

// ── OpenTelemetry — tracing + métricas Prometheus ─────────────────────────────
builder.Services.AddOpenTelemetryObservability("AntiFraude.Api");

// ── Infrastructure (EF Core, repos, MassTransit, rules, use cases) ────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Swagger / OpenAPI ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Health Checks ──────────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Postgres") ?? string.Empty,
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "ready"])
    .AddRabbitMQ(
        async sp =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                Uri = new Uri(builder.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@rabbitmq:5672")
            };
            return await factory.CreateConnectionAsync();
        },
        name: "rabbitmq",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["messaging", "ready"]);

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCorrelationIdLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapTransactionEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// /metrics — exposto pelo OpenTelemetry Prometheus exporter
app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program { }
