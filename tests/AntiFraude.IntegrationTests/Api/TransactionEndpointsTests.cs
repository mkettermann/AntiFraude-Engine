using System.Net;
using System.Net.Http.Json;
using AntiFraude.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AntiFraude.IntegrationTests.Api;

/// <summary>
/// Testes de integração dos endpoints de transação.
/// Requerem PostgreSQL e RabbitMQ via TestContainers — configure as variáveis
/// de ambiente ou o docker-compose de teste antes de rodar esta suite.
///
/// Para rodar com TestContainers:
///   dotnet test --filter "Category=Integration"
/// </summary>
public class TransactionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TransactionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostTransaction_WithValidPayloadAndIdempotencyKey_Returns202()
    {
        // Arrange
        var request = new TransactionRequest(
            TransactionId: $"TXN-{Guid.NewGuid():N}",
            Amount: 500m,
            MerchantId: "MRC-001",
            CustomerId: "CUS-001",
            Currency: "BRL");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/transactions");
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostTransaction_WithoutIdempotencyKey_Returns422()
    {
        // Arrange
        var request = new TransactionRequest(
            TransactionId: $"TXN-{Guid.NewGuid():N}",
            Amount: 100m,
            MerchantId: "MRC-001",
            CustomerId: "CUS-001",
            Currency: "BRL");

        // Act
        var response = await _client.PostAsJsonAsync("/transactions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostTransaction_WithSameIdempotencyKey_Returns200OnSecondCall()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new TransactionRequest(
            TransactionId: $"TXN-{Guid.NewGuid():N}",
            Amount: 200m,
            MerchantId: "MRC-001",
            CustomerId: "CUS-001",
            Currency: "BRL");

        // Act — primeira chamada
        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/transactions");
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        request1.Content = JsonContent.Create(request);
        var response1 = await _client.SendAsync(request1);

        // Act — segunda chamada com mesma chave
        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/transactions");
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        request2.Content = JsonContent.Create(request);
        var response2 = await _client.SendAsync(request2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response2.StatusCode.Should().Be(HttpStatusCode.OK); // replay idempotente
    }

    [Fact]
    public async Task GetTransaction_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/transactions/TXN-NAO-EXISTE");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHealth_Returns200WithDependencyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
    }
}
