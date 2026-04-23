using AntiFraude.Application.Interfaces;
using AntiFraude.Application.Services;
using AntiFraude.Application.UseCases;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Domain.Rules;
using AntiFraude.Infrastructure.Data;
using AntiFraude.Infrastructure.ExternalServices;
using AntiFraude.Infrastructure.Messaging;
using AntiFraude.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AntiFraude.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureMassTransit = null)
    {
        // ── Entity Framework Core ─────────────────────────────────────────────────
        // LIFETIME: DbContext é Scoped — um contexto por request HTTP / mensagem.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // ── Repositórios ──────────────────────────────────────────────────────────
        // LIFETIME: Scoped — compartilham o DbContext do scope corrente.
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();

        // ── Outbox Pattern ────────────────────────────────────────────────────────
        // LIFETIME: Scoped — usa o mesmo DbContext da unidade de trabalho atual.
        services.AddScoped<IOutboxPublisher, OutboxPublisher>();
        // LIFETIME: Singleton — BackgroundService único gerenciado pelo host.
        services.AddHostedService<OutboxRelayService>();

        // ── Regras de Fraude ──────────────────────────────────────────────────────
        // LIFETIME: Transient — cada avaliação recebe instâncias isoladas das regras,
        // evitando qualquer compartilhamento de estado entre transações concorrentes.
        // Adicione novas regras aqui: services.AddTransient<IFraudRule, MinhaRegra>();
        services.AddTransient<IFraudRule, AmountLimitRule>();

        // LIFETIME: Scoped — FraudEvaluationService não mantém estado próprio;
        // o escopo coincide com o request/mensagem sendo processado.
        services.AddScoped<IFraudEvaluationService, FraudEvaluationService>();

        // ── Use Cases ─────────────────────────────────────────────────────────────
        // LIFETIME: Scoped — ciclo de vida alinhado ao request HTTP.
        services.AddScoped<SubmitTransactionUseCase>();
        services.AddScoped<GetTransactionUseCase>();

        // ── Serviço externo de score de crédito ───────────────────────────────────
        // LIFETIME: HttpClient gerenciado pelo IHttpClientFactory (evita socket exhaustion).
        // ExternalScoreClient é Singleton pois mantém estado do Circuit Breaker.
        services.AddHttpClient<ExternalScoreClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["ExternalServices:ScoreServiceUrl"]
                ?? "http://score-service:8080");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // ── MassTransit / RabbitMQ ────────────────────────────────────────────────
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            // Permite que projetos consumidores (Worker) registrem seus consumers
            configureMassTransit?.Invoke(x);

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
                {
                    h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                    h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                });

                // Exchange tipo "direct" conforme decisão arquitetural
                // Retry: 3 tentativas com intervalo de 5s; após isso → Dead Letter Queue
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
