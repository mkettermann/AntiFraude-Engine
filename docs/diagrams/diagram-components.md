# Diagrama de Componentes — AntiFraude Engine

```mermaid
C4Component
    title Diagrama de Componentes — AntiFraude Engine

    Person(client, "Cliente", "Sistema ou usuário que submete transações via HTTP")

    System_Boundary(antifraude, "AntiFraude Engine") {

        Container_Boundary(api_container, "AntiFraude.Api [:8080]") {
            Component(endpoint, "TransactionEndpoints", "Minimal API", "POST /transactions, GET /transactions/{id}, GET /health, GET /metrics")
            Component(idempotency, "SubmitTransactionUseCase", "C# / Use Case", "Verifica idempotência e orquestra persistência + enfileiramento")
            Component(correlation_mw, "CorrelationIdMiddleware", "ASP.NET Middleware", "Gera/propaga X-Correlation-Id em todos os logs do request")
        }

        Container_Boundary(worker_container, "AntiFraude.Worker") {
            Component(consumer, "TransactionConsumer", "MassTransit IConsumer", "Consome fila, loga RECEIVED→PROCESSING→decisão, persiste resultado")
            Component(fraud_svc, "FraudEvaluationService", "C# / Application Service", "Orquestra IEnumerable<IFraudRule> — fail-fast na primeira rejeição")
            Component(amount_rule, "AmountLimitRule", "IFraudRule [Transient]", "Amount > 10.000 → REJECTED (AMOUNT_EXCEEDS_LIMIT)")
        }

        Container_Boundary(infra_container, "AntiFraude.Infrastructure") {
            Component(outbox_pub, "OutboxPublisher", "IOutboxPublisher [Scoped]", "Persiste OutboxMessage na mesma UoW antes de publicar")
            Component(outbox_relay, "OutboxRelayService", "BackgroundService", "Lê OutboxMessages pendentes e publica no RabbitMQ a cada 5s")
            Component(score_client, "ExternalScoreClient", "HttpClient + Polly", "Circuit Breaker: 3 falhas → 30s aberto; fallback = REVIEW")
            Component(ef_repos, "Repositories", "EF Core [Scoped]", "TransactionRepository, AuditLogRepository, ProcessedMessageRepository")
        }

    }

    ContainerDb(postgres, "PostgreSQL :5432", "PostgreSQL 16", "Transactions, AuditLogs, ProcessedMessages, OutboxMessages")
    ContainerDb(rabbitmq, "RabbitMQ :5672", "RabbitMQ 3.13", "Exchange: antifraude (direct)\nQueue: transaction-submitted\nDLQ: antifraude-dlq")
    System_Ext(score_service, "Score Service (externo)", "Serviço de score de crédito simulado")

    Rel(client, endpoint, "POST /transactions\nIdempotency-Key header", "HTTPS")
    Rel(endpoint, idempotency, "chama")
    Rel(idempotency, ef_repos, "verifica ProcessedMessages\npersiste Transaction")
    Rel(idempotency, outbox_pub, "publica evento no Outbox")
    Rel(outbox_pub, postgres, "INSERT OutboxMessage", "mesma transação DB")
    Rel(outbox_relay, postgres, "SELECT OutboxMessages WHERE ProcessedAt IS NULL")
    Rel(outbox_relay, rabbitmq, "PUBLISH TransactionSubmittedEvent", "MassTransit AMQP")
    Rel(rabbitmq, consumer, "CONSUME", "MassTransit")
    Rel(consumer, fraud_svc, "EvaluateAsync(transaction)")
    Rel(fraud_svc, amount_rule, "EvaluateAsync — Transient")
    Rel(fraud_svc, score_client, "GetScoreAsync(customerId)")
    Rel(score_client, score_service, "GET /score/{customerId}", "HTTP + Circuit Breaker")
    Rel(consumer, ef_repos, "UpdateAsync(transaction)\nAddAsync(auditLog)")
    Rel(ef_repos, postgres, "SQL queries", "EF Core / Npgsql")
    Rel(correlation_mw, consumer, "CorrelationId propagado via\nTransactionSubmittedEvent.CorrelationId")
```

## Legenda

| Componente | Lifetime DI | Descrição |
|---|---|---|
| `CorrelationIdMiddleware` | Singleton (middleware) | Enriquece Serilog LogContext com CorrelationId |
| `AmountLimitRule` | **Transient** | Instância isolada por avaliação; sem estado compartilhado |
| `FraudEvaluationService` | Scoped | Orquestrador de regras por request/mensagem |
| `OutboxPublisher` | Scoped | Mesma UoW do DbContext |
| `OutboxRelayService` | Singleton (hosted) | BackgroundService do host |
| `ExternalScoreClient` | Singleton (HttpClient) | Estado do Circuit Breaker compartilhado globalmente |
| `TransactionRepository` | Scoped | Mesmo DbContext do escopo |
