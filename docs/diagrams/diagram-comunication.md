# Arquitetura de comunicação

```mermaid
flowchart LR
    UI[Frontend UI] --> API[Backend API]
    API --> RMQ[(RabbitMQ)]
    API --> PG[(PostgreSQL)]
    RMQ --> WOR[Worker]
    WOR --> PG
```
