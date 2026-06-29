# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projeto

Servidor MCP (Model Context Protocol) para integração com o Trello, exposto via HTTP usando ASP.NET Core 10 e a biblioteca `ModelContextProtocol.AspNetCore`.

## Comandos

```bash
# Rodar o servidor (perfil http)
dotnet run --project TrelloMcpHttp/TrelloMcpHttp.csproj --launch-profile http

# Rodar com HTTPS
dotnet run --project TrelloMcpHttp/TrelloMcpHttp.csproj --launch-profile https
```

O servidor sobe em `http://localhost:5123` (http) ou `https://localhost:7138` (https). O endpoint MCP fica em `/mcp`.

## Configuração

As credenciais do Trello são configuradas em `appsettings.json` (ou via variáveis de ambiente) na seção `Trello`:

```json
{
  "Trello": {
    "ApiKey": "<sua-api-key>",
    "Token": "<seu-token>"
  }
}
```

Nunca commitar credenciais reais. Use `appsettings.Development.json` (ignorado pelo git) ou variáveis de ambiente `Trello__ApiKey` e `Trello__Token`.

## Arquitetura

- **`Program.cs`** — bootstrap: registra `TrelloOptions`, `TrelloClient` (via `IHttpClientFactory`) e o servidor MCP com transporte HTTP.
- **`TrelloOptions.cs`** — POCO que mapeia a seção `Trello` do `appsettings.json` (`ApiKey`, `Token`).
- **`TrelloClient.cs`** — wrapper sobre `HttpClient` que chama a API REST do Trello (`https://api.trello.com/1/`). Todos os parâmetros de autenticação são appendados na query string via `Auth()`.
- **`TrelloTools.cs`** — classe anotada com `[McpServerToolType]` que expõe as ferramentas MCP. Cada método público anotado com `[McpServerTool]` vira uma tool disponível para clientes MCP.

## Adicionando novas tools

1. Adicionar o método em `TrelloClient.cs` chamando o endpoint REST do Trello.
2. Adicionar o método correspondente em `TrelloTools.cs` com `[McpServerTool]` e `[Description(...)]` nos parâmetros.

## Stack

- .NET 10 / ASP.NET Core 10
- `ModelContextProtocol.AspNetCore` 1.4.0
- `Microsoft.AspNetCore.OpenApi` 10.0.9
- Sem banco de dados — stateless, apenas proxy para a API do Trello
