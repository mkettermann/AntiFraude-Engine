# AntiFraude Engine — Frontend Angular

Interface web para consulta e envio de transações ao motor antifraude. Consome a API REST do backend em `http://localhost:8080`.

Gerado com [Angular CLI](https://github.com/angular/angular-cli) versão 21.2.7.

---

## Pré-requisitos

- [Node.js](https://nodejs.org/) >= 20.x
- [npm](https://www.npmjs.com/) >= 10.x (incluído no Node.js)
- Angular CLI instalado globalmente:

```bash
npm install -g @angular/cli
```

---

## Instalação

Na pasta do projeto Angular, instale as dependências:

```bash
cd angular/AntiFraudeEngine
npm install
```

---

## Como Utilizar

### 1. Subir o backend

Antes de iniciar o frontend, certifique-se de que a API e a infraestrutura estão rodando. Consulte o [README principal](../../README.md) para as instruções completas.

```bash
# A partir da raiz do repositório
docker compose -f infra/docker-compose.yml up -d postgres rabbitmq

cd src/AntiFraude.Api
dotnet run
```

### 2. Iniciar o servidor de desenvolvimento

```bash
cd angular/AntiFraudeEngine
ng serve
```

Abra o navegador em `http://localhost:4200/`. A aplicação recarrega automaticamente ao salvar qualquer arquivo fonte.

---

## Build de Produção

```bash
ng build
```

Os artefatos são gerados na pasta `dist/`. O build de produção aplica otimizações de desempenho automaticamente.

---

## Testes

### Testes unitários

```bash
ng test
```

Executa os testes com o [Vitest](https://vitest.dev/).

### Testes end-to-end

```bash
ng e2e
```

> O Angular CLI não inclui um framework de e2e por padrão. Configure o de sua preferência.

---

## Geração de Componentes

```bash
ng generate component nome-do-componente
```

Para ver todas as opções disponíveis:

```bash
ng generate --help
```

---

## Recursos Adicionais

- [Documentação oficial do Angular CLI](https://angular.dev/tools/cli)
