# AntiFraude Engine

Motor de avaliação antifraude para processamento de transações financeiras em tempo real. Retorna decisões `APPROVED`, `REJECTED` ou `REVIEW` com rastreabilidade completa de cada etapa.

---

## Índice

1. [Visão Geral](#visão-geral)
2. [Como Rodar Localmente](#como-rodar-localmente)
3. [Fluxo de Ponta a Ponta](#fluxo-de-ponta-a-ponta)
4. [Contrato de API](#contrato-de-api)
5. [Observabilidade](#observabilidade)
6. [Resiliência](#resiliência)
7. [Idempotência](#idempotência)
8. [Injeção de Dependência — IFraudRule como Transient](#injeção-de-dependência--ifraudrule-como-transient)
9. [Estrutura do Repositório](#estrutura-do-repositório)
10. [ADRs e Diagramas](#adrs-e-diagramas)

---

## Visão Geral

O AntiFraude Engine é composto por dois processos independentes que se comunicam via mensageria assíncrona:

| Componente | Responsabilidade |
|---|---|
| **AntiFraude.Api** | Recebe requisições HTTP, valida idempotência e enfileira transações |
| **AntiFraude.Worker** | Consome a fila, executa regras de fraude, persiste decisões e registra auditoria |

**Stack:** .NET 9 · PostgreSQL · RabbitMQ · MassTransit · Entity Framework Core · Serilog · OpenTelemetry · Polly

---

## Como Rodar Localmente

### Pré-requisitos

- [Docker Desktop](https://docs.docker.com/desktop/) >= 4.x
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 1. Subir infraestrutura

```bash
docker compose -f infra/docker-compose.yml up -d postgres rabbitmq
```

Aguarde os health checks ficarem `healthy`:

```bash
docker compose -f infra/docker-compose.yml ps
```

### 2. Aplicar migrations

> **Importante:** migrations nunca são aplicadas automaticamente em produção.
> Execute manualmente em cada deploy:

```bash
cd src/AntiFraude.Api
dotnet ef database update --project ../AntiFraude.Infrastructure/AntiFraude.Infrastructure.csproj
```

Para criar uma nova migration após mudanças de modelo:

```bash
dotnet ef migrations add NomeDaMigration \
  --project ../AntiFraude.Infrastructure/AntiFraude.Infrastructure.csproj \
  --startup-project .
```

### 3. Rodar a solução completa via Docker Compose

```bash
docker compose -f infra/docker-compose.yml up --build
```

Serviços disponíveis:

| URL | Descrição |
|---|---|
| `http://localhost:8080/swagger` | Swagger UI da API |
| `http://localhost:8080/health` | Health check completo (JSON) |
| `http://localhost:8080/metrics` | Endpoint Prometheus |
| `http://localhost:15672` | RabbitMQ Management UI (guest/guest) |

### 4. Rodar em modo desenvolvimento (sem Docker)

```bash
# Terminal 1 — API
cd src/AntiFraude.Api
dotnet run

# Terminal 2 — Worker
cd src/AntiFraude.Worker
dotnet run

# Terminal 3 — Testes
dotnet test
```

---

## Fluxo de Ponta a Ponta

### Cenário: transação de R$ 15.000 (REJECTED)

**1. Cliente envia a requisição:**

```http
POST /transactions HTTP/1.1
Host: localhost:8080
Content-Type: application/json
Idempotency-Key: txn-cliente-abc-001

{
  "transactionId": "TXN-2026-001",
  "amount": 15000,
  "merchantId": "MRC-001",
  "customerId": "CUS-999",
  "currency": "BRL"
}
```

**2. API valida e persiste:**

- Verifica se o `Idempotency-Key` já foi processado (tabela `ProcessedMessages`)
- Cria a `Transaction` com status `RECEIVED` no banco
- Salva um `OutboxMessage` na mesma transação de banco (Outbox Pattern)
- Retorna `HTTP 202 Accepted` imediatamente

**3. Outbox Relay publica:**

- `OutboxRelayService` (BackgroundService) lê mensagens pendentes a cada 5 segundos
- Publica `TransactionSubmittedEvent` no RabbitMQ via MassTransit (exchange `direct`)

**4. Worker consome:**

- `TransactionConsumer` recebe a mensagem
- Registra no log: `Transaction TXN-2026-001 RECEIVED by worker`
- Transição de status: `RECEIVED → PROCESSING` (persiste + registra `AuditLog`)

**5. Avaliação pelas regras:**

- `FraudEvaluationService` executa `AmountLimitRule`
- `AmountLimitRule`: Amount (15.000) > 10.000 → `IsRejected=true`, Reason=`AMOUNT_EXCEEDS_LIMIT`
- Primeira regra que rejeita encerra a avaliação (fail-fast)

**6. Decisão final:**

- Status: `PROCESSING → REJECTED`
- `Transaction.Decision = REJECTED`, `Reason = "AMOUNT_EXCEEDS_LIMIT"`, `ProcessedAt = now`
- `AuditLog` registrado com a transição de status e motivo
- Log estruturado: `Transaction TXN-2026-001 PROCESSING → REJECTED in 12ms`

**7. Cliente consulta o resultado:**

```http
GET /transactions/TXN-2026-001

HTTP/1.1 200 OK
{
  "transactionId": "TXN-2026-001",
  "status": "REJECTED",
  "decision": "REJECTED",
  "reason": "AMOUNT_EXCEEDS_LIMIT",
  "processedAt": "2026-04-23T15:00:01Z",
  "auditTrail": [
    { "fromStatus": "RECEIVED",    "toStatus": "PROCESSING", "occurredAt": "...", "message": "Transaction picked up by worker" },
    { "fromStatus": "PROCESSING",  "toStatus": "REJECTED",   "occurredAt": "...", "message": "Decision=REJECTED Reason=AMOUNT_EXCEEDS_LIMIT" }
  ]
}
```

---

## Contrato de API

### `POST /transactions`

Enfileira uma transação para avaliação antifraude.

**Header obrigatório:** `Idempotency-Key: <string-única>`

**Body:**

```json
{
  "transactionId": "string",
  "amount": 0.00,
  "merchantId": "string",
  "customerId": "string",
  "currency": "BRL",
  "metadata": { "key": "value" }
}
```

| Status | Descrição |
|---|---|
| `202 Accepted` | Transação enfileirada para processamento |
| `200 OK` | Idempotência — requisição já processada, retorna resposta original |
| `422 Unprocessable Entity` | Payload inválido ou Idempotency-Key ausente |

---

### `GET /transactions/{transactionId}`

Retorna o estado atual e trilha de auditoria.

**Response `200 OK`:**

```json
{
  "transactionId": "string",
  "status": "REJECTED",
  "decision": "REJECTED",
  "reason": "AMOUNT_EXCEEDS_LIMIT",
  "createdAt": "2026-04-23T15:00:00Z",
  "processedAt": "2026-04-23T15:00:01Z",
  "auditTrail": [
    {
      "fromStatus": "RECEIVED",
      "toStatus": "PROCESSING",
      "occurredAt": "2026-04-23T15:00:00.5Z",
      "message": "string"
    }
  ]
}
```

---

### `GET /health`

Retorna o status de saúde da API e suas dependências (PostgreSQL, RabbitMQ).

---

### `GET /metrics`

Endpoint compatível com Prometheus. Configure o scrape em `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: antifraude-api
    static_configs:
      - targets: ['api:8080']
    metrics_path: /metrics
```

---

## Observabilidade

### Logs Estruturados (Serilog)

Todos os logs incluem os campos `CorrelationId` e `TransactionId` (via `LogContext.PushProperty`).

Exemplos de eventos registrados:

```
[15:00:00 INF] [corr-abc123] [TXN-2026-001] Transaction TXN-2026-001 RECEIVED — Amount=15000 Currency=BRL
[15:00:00 INF] [corr-abc123] [TXN-2026-001] Transaction TXN-2026-001 status: RECEIVED → PROCESSING
[15:00:00 INF] [corr-abc123] [TXN-2026-001] Rule AmountLimitRule evaluated: IsRejected=True Reason=AMOUNT_EXCEEDS_LIMIT in 0ms
[15:00:00 WRN] [corr-abc123] [TXN-2026-001] Transaction TXN-2026-001 REJECTED by rule AmountLimitRule after 1ms
[15:00:00 INF] [corr-abc123] [TXN-2026-001] Transaction TXN-2026-001 PROCESSING → REJECTED in 5ms
```

### Métricas (Prometheus)

Disponíveis em `GET /metrics`. Métricas incluídas:

- `http_server_request_duration_seconds` — latência por endpoint
- `http_client_request_duration_seconds` — latência de chamadas externas
- `dotnet_runtime_*` — métricas de runtime (.NET)
- `process_*` — CPU, memória

### Tracing Distribuído (OpenTelemetry)

Configure `OTEL_EXPORTER_OTLP_ENDPOINT` para exportar traces para Jaeger ou Grafana Tempo:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
```

Cada requisição gera um trace com spans para:

- Endpoint HTTP
- Queries EF Core ao PostgreSQL
- Chamadas HTTP ao serviço externo de score

---

## Resiliência

### Retry + Dead Letter Queue (RabbitMQ)

O `TransactionConsumer` está configurado com 3 tentativas automáticas de reprocessamento (intervalo de 5s entre cada). Após a 3ª falha, MassTransit encaminha a mensagem para a **Dead Letter Queue** (`antifraude-dlq`).

Monitore mensagens na DLQ via RabbitMQ Management UI em `http://localhost:15672`.

### Circuit Breaker (Polly)

`ExternalScoreClient` usa Circuit Breaker do Polly:

| Estado | Condição | Comportamento |
|---|---|---|
| **CLOSED** | Normal | Chamadas passam normalmente |
| **OPEN** | 3 falhas consecutivas | Circuito abre por **30 segundos** |
| **HALF-OPEN** | Após 30s | 1 chamada teste — fecha se bem-sucedida |

**Fallback:** enquanto o circuito está aberto, a decisão retornada é automaticamente `REVIEW` (revisão manual), evitando que uma dependência externa derrube toda a avaliação.

---

## Idempotência

O sistema garante que a mesma transação não seja processada duas vezes, mesmo que o cliente reenvie a requisição.

**Fluxo:**

```
POST /transactions
  Header: Idempotency-Key: txn-abc-001
  │
  ├─ Hash SHA-256 da chave calculado
  ├─ Busca em ProcessedMessages por IdempotencyKeyHash
  │
  ├─ [NÃO ENCONTRADO] → processa normalmente → salva ProcessedMessage → HTTP 202
  └─ [ENCONTRADO]     → retorna ResponsePayload salvo               → HTTP 200
```

A tabela `ProcessedMessages` possui um índice `UNIQUE` em `IdempotencyKeyHash`, garantindo proteção contra race conditions a nível de banco de dados — mesmo que dois requests idênticos cheguem simultaneamente.

---

## Injeção de Dependência — IFraudRule como Transient

Todas as implementações de `IFraudRule` são registradas com lifetime **Transient**:

```csharp
// InfrastructureServiceExtensions.cs
// LIFETIME: Transient — cada avaliação recebe instâncias isoladas das regras,
// evitando qualquer compartilhamento de estado entre transações concorrentes.
services.AddTransient<IFraudRule, AmountLimitRule>();
```

**Por que Transient?**

Regras de fraude podem ter estado interno (acumuladores, contexto de avaliação). Ao usar Transient:

- Cada chamada a `FraudEvaluationService.EvaluateAsync()` recebe instâncias novas
- Não há risco de dados de uma transação "vazarem" para outra em cenários de alta concorrência
- Adicionar novas regras stateful no futuro não exige revisão de DI

Para adicionar uma nova regra:

1. Implemente `IFraudRule` em `AntiFraude.Domain/Rules/`
2. Registre em `InfrastructureServiceExtensions.cs`: `services.AddTransient<IFraudRule, MinhaRegra>();`
3. A regra é automaticamente incluída na avaliação via `IEnumerable<IFraudRule>`

---

## Estrutura do Repositório

```
antifraude-engine/
├── src/
│   ├── AntiFraude.Api/           # ASP.NET Core Minimal API — entrada HTTP
│   ├── AntiFraude.Worker/        # BackgroundService — consome fila RabbitMQ
│   ├── AntiFraude.Domain/        # Entidades, regras, enums, interfaces
│   ├── AntiFraude.Application/   # Use cases, DTOs, orquestração
│   ├── AntiFraude.Infrastructure/# EF Core, repositórios, RabbitMQ, HTTP clients
│   └── AntiFraude.CrossCutting/  # Logging, correlationId middleware, extensões
├── tests/
│   ├── AntiFraude.UnitTests/     # xUnit + Moq — regras de negócio isoladas
│   └── AntiFraude.IntegrationTests/ # WebApplicationFactory + TestContainers
├── docs/
│   ├── adr/                      # Architectural Decision Records
│   └── diagrams/                 # Diagramas Mermaid
├── infra/
│   └── docker-compose.yml
└── README.md
```

---

## ADRs e Diagramas

| Documento | Descrição |
|---|---|
| [ADR-001](docs/adr/ADR-001.md) | Escolha do RabbitMQ como broker de mensageria |
| [ADR-002](docs/adr/ADR-002.md) | Escolha do PostgreSQL como banco relacional |
| [ADR-003](docs/adr/ADR-003.md) | Estratégia de idempotência com Idempotency-Key |
| [ADR-004](docs/adr/ADR-004.md) | Deployment com Docker Compose e Kubernetes readiness |
| [Diagrama de Componentes](docs/diagrams/diagram-components.md) | Visão arquitetural dos componentes |
| [Diagrama de Sequência](docs/diagrams/diagram-sequence.md) | Fluxo completo de uma transação REJECTED |
