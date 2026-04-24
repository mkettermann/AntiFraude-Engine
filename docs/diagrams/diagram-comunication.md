# Arquitetura de comunicação

```mermaid
flowchart LR
    UI[Frontend UI] --> API[Backend API]
    API --> WOR[Worker API]
    API --> PGF[(PostgreSQL - Faturamento)]
```
