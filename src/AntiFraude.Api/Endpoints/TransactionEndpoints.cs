using AntiFraude.Application.DTOs;
using AntiFraude.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace AntiFraude.Api.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions").WithTags("Transactions");

        // POST /transactions
        group.MapPost("/", async (
            [FromBody] TransactionRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            SubmitTransactionUseCase useCase,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.UnprocessableEntity(new { error = "Header 'Idempotency-Key' is required." });

            if (string.IsNullOrWhiteSpace(request.TransactionId)
                || request.Amount <= 0
                || string.IsNullOrWhiteSpace(request.MerchantId)
                || string.IsNullOrWhiteSpace(request.CustomerId)
                || string.IsNullOrWhiteSpace(request.Currency))
            {
                return Results.UnprocessableEntity(new { error = "Invalid request payload." });
            }

            var (response, isIdempotentReplay) = await useCase.ExecuteAsync(request, idempotencyKey, ct);

            return isIdempotentReplay
                ? Results.Ok(response)         // 200 — replay idempotente
                : Results.Accepted(value: response); // 202 — enfileirado
        })
        .WithName("SubmitTransaction")
        .WithSummary("Submit a transaction for fraud evaluation")
        .Produces<TransactionResponse>(StatusCodes.Status202Accepted)
        .Produces<TransactionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // GET /transactions/{id}
        group.MapGet("/{transactionId}", async (
            string transactionId,
            GetTransactionUseCase useCase,
            CancellationToken ct) =>
        {
            var response = await useCase.ExecuteAsync(transactionId, ct);

            return response is null
                ? Results.NotFound(new { error = $"Transaction '{transactionId}' not found." })
                : Results.Ok(response);
        })
        .WithName("GetTransaction")
        .WithSummary("Get transaction details and audit trail")
        .Produces<TransactionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
