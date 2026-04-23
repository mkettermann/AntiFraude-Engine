using AntiFraude.Application.UseCases;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace AntiFraude.CrossCutting.Middleware;

/// <summary>
/// Middleware que extrai ou gera um Correlation ID para cada request HTTP e o adiciona:
///   1. Ao Serilog LogContext — todos os logs do request incluirão o campo CorrelationId
///   2. Ao CorrelationContext ambient — disponível para use cases sem injeção explícita
///   3. Ao header de resposta X-Correlation-Id — facilita rastreamento pelo cliente
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Enriquece todos os logs Serilog deste request com o correlationId
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Disponibiliza para use cases via AsyncLocal
            CorrelationContext.Current = correlationId;

            // Propaga o correlationId no response para rastreamento pelo caller
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            await _next(context);
        }
    }
}
