# Desafio — Case Resumido

> **Objetivo**  
> Avaliar sua capacidade de **propor soluções arquiteturais**, documentar decisões e estruturar um raciocínio técnico para cenários de alta complexidade.

---

## 1) Contexto

A empresa precisa de um **módulo de avaliação antifraude** para processar transações financeiras.  
Esse módulo deve receber transações, aplicar regras e devolver uma decisão (`APPROVED`, `REJECTED`, `REVIEW`).  
O sistema deve ser **idempotente**, **resiliente** e **auditável**.

---

## 2) Escopo do Entregável

### A. Documentação (em Markdown)

- **Visão geral** da solução (arquitetura, componentes principais).  
- **Fluxo de ponta a ponta**: da entrada da transação até a decisão.  
- **Pontos de resiliência** (retry, backoff, DLQ, fallback).  
- **Idempotência e Deduplicação**: como seriam tratados.  
- **Observabilidade**: métricas, logs estruturados e tracing.

### B. Diagramas (mínimo 2)

1. **Diagrama de Componentes/Containers** (API, fila, worker, banco de dados, integrações).  
2. **Diagrama de Sequência** ou **Fluxo** mostrando o processamento de uma transação.

### C. Decisões Arquiteturais (ADRs)

Liste pelo menos **3 decisões**, incluindo:

- Escolha de mensageria (RabbitMQ, Kafka, SQS/SNS).  
- Escolha de banco de dados (relacional vs NoSQL).  
- Estratégia de idempotência/dedup.  
- *(opcional extra)* Estratégia de deployment (K8s, serverless, containers).

### D. Contrato de API (mínimo)

- Descreva endpoints principais:  
  - `POST /transactions` (Idempotency-Key obrigatória).  
  - `GET /transactions/{id}` (status e decisão).

---

## 4) Entregável

- Arquivo `README.md` com toda a documentação.  
- Diagramas em formato `.md` (Mermaid) ou `.png/.svg`.  
- ADRs em seção separada dentro do README ou em arquivos próprios.

---
