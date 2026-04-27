using System.Net.Http.Json;
using AntiFraude.Domain.Enums;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace AntiFraude.Infrastructure.ExternalServices;

/// <summary>
/// Cliente HTTP para o serviço externo de score de crédito.
///
/// RESILIÊNCIA: Circuit Breaker com Polly v8 (ResiliencePipeline).
///   - 3 falhas consecutivas (FailureRatio=1.0, MinimumThroughput=3) → circuito ABERTO por 30s
///   - Enquanto aberto, BrokenCircuitException é capturada → fallback retorna REVIEW
///   - Após 30s, circuito vai para HALF-OPEN e testa uma requisição
/// </summary>
// LIFETIME: Singleton — o pipeline de resiliência mantém estado do circuit breaker entre requests.
public sealed class ExternalScoreClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalScoreClient> _logger;
    private readonly ResiliencePipeline _pipeline;

    public ExternalScoreClient(HttpClient httpClient, ILogger<ExternalScoreClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Falha se 100% das chamadas dentro de uma janela de 3 falham
                FailureRatio = 1.0,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "ExternalScoreClient circuit OPENED for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("ExternalScoreClient circuit CLOSED — normal operation resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("ExternalScoreClient circuit HALF-OPEN — testing connectivity");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<(TransactionDecision Decision, int? Score)> GetScoreAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ExternalScoreResponse? response = null;

            await _pipeline.ExecuteAsync(async ct =>
            {
                var httpResponse = await _httpClient.GetAsync(
                    $"/score/{Uri.EscapeDataString(customerId)}", ct);

                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<ExternalScoreResponse>(
                    cancellationToken: ct)
                    ?? throw new InvalidOperationException("Empty response from score service");
            }, cancellationToken);

            if (response is null)
                return (TransactionDecision.REVIEW, null);

            _logger.LogInformation(
                "ExternalScore for CustomerId {CustomerId}: Score={Score}",
                customerId, response.Score);

            var decision = response.Score switch
            {
                < 300 => TransactionDecision.REJECTED,
                < 600 => TransactionDecision.REVIEW,
                _     => TransactionDecision.APPROVED
            };

            return (decision, response.Score);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "ExternalScoreClient circuit is OPEN — returning REVIEW fallback for CustomerId {CustomerId}",
                customerId);

            // FALLBACK: circuit aberto → decisão conservadora para revisão manual
            return (TransactionDecision.REVIEW, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error calling ExternalScoreService for CustomerId {CustomerId}",
                customerId);

            return (TransactionDecision.REVIEW, null);
        }
    }
}

public sealed record ExternalScoreResponse(int Score, string RiskLevel);
