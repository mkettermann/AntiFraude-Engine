using AntiFraude.CrossCutting.Extensions;
using AntiFraude.Infrastructure.Extensions;
using AntiFraude.Worker.Consumers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────────
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{TransactionId}] {Message:lj}{NewLine}{Exception}"));

// ── OpenTelemetry ──────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetryObservability("AntiFraude.Worker");

// ── Infrastructure + MassTransit com consumer registrado ──────────────────────
builder.Services.AddInfrastructure(
    builder.Configuration,
    configureMassTransit: x =>
    {
        // Registra o consumer no bus — MassTransit cria a fila automaticamente
        // DLQ é configurada pelo UseMessageRetry (3 tentativas) em InfrastructureServiceExtensions
        x.AddConsumer<TransactionConsumer>();
    });

var host = builder.Build();
host.Run();

