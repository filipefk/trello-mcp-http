# trello-mcp-http

Servidor MCP (Model Context Protocol) para integração com o Trello, exposto via HTTP usando ASP.NET Core 10 e a biblioteca `ModelContextProtocol.AspNetCore`.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Conta no Trello com [API Key e Token](https://trello.com/power-ups/admin) gerados

## Obtendo as credenciais do Trello

Para usar o servidor, você precisará de uma chave de API e um token do Trello. Siga os passos abaixo para obtê-los:

### 1. Criar um aplicativo no Trello

1. Faça login no Trello e acesse [https://trello.com/power-ups/admin/](https://trello.com/power-ups/admin/)
2. Clique no botão **"Novo"** para criar um novo aplicativo
3. Preencha os campos solicitados — o campo "URL de conector Iframe" não é obrigatório
4. Clique em **"Gerar nova chave de API"**
5. O valor exibido no campo **"Chave de API"** é o que você usará como `X-Trello-Api-Key`

### 2. Gerar o token de autenticação

Monte a URL abaixo substituindo os placeholders pelos dados do seu aplicativo:

```
https://trello.com/1/authorize?expiration=never&scope=read,write&response_type=token&name=[nome+do+app]&key=[Chave de API]
```

> **Atenção:** no parâmetro `name`, substitua espaços por `+` (ex.: `Meu App` → `Meu+App`).

1. Com o Trello aberto no browser, acesse a URL montada acima
2. Leia as permissões solicitadas e clique em **"Permitir"**
3. Copie o token exibido na página seguinte, que será o valor de `X-Trello-Token`

## Configuração

### Credenciais por requisição (recomendado)

Passe as credenciais como headers HTTP em cada requisição. Isso permite que diferentes clientes usem suas próprias credenciais sem nenhuma configuração no servidor:

```
X-Trello-Api-Key: <sua-api-key>
X-Trello-Token: <seu-token>
```

### Credenciais globais (fallback)

Crie o arquivo `TrelloMcpHttp/appsettings.Development.json` (ignorado pelo git) com suas credenciais:

```json
{
  "Trello": {
    "ApiKey": "<sua-api-key>",
    "Token": "<seu-token>"
  }
}
```

Alternativamente, use variáveis de ambiente `Trello__ApiKey` e `Trello__Token`.

O endpoint MCP fica disponível em `http://localhost:5123/mcp` ou `https://localhost:7138/mcp`.

> **Aviso — HTTPS:** Em ambiente de desenvolvimento, prefira usar `http://localhost:5123/mcp` para evitar problemas com certificados. A url `https://localhost:7138/mcp` funciona bem no Postman mas é mais complicado no Claude Code.

### Conectando via Claude Code CLI

Para adicionar o MCP diretamente pelo terminal do Claude Code:

```bash
# Adicionar globalmente (disponível em todos os projetos)
claude mcp add --scope user trello --transport http http://localhost:5123/mcp --header "X-Trello-Api-Key: sua-api-key" --header "X-Trello-Token: seu-token"

# Adicionar no projeto atual
claude mcp add trello --transport http http://localhost:5123/mcp --header "X-Trello-Api-Key: sua-api-key" --header "X-Trello-Token: seu-token"
```

Outros comandos úteis:

```bash
claude mcp list          # Lista MCPs configurados e testa a conectividade
claude mcp get trello    # Ver detalhes do MCP
claude mcp remove trello # Remover o MCP
```

## Tools disponíveis

| Tool | Descrição |
|------|-----------|
| `get_boards` | Lista todos os boards do usuário autenticado |
| `get_lists` | Lista as colunas (lists) de um board |
| `get_cards` | Lista os cards de uma coluna |
| `get_card` | Busca um card pelo ID |
| `get_board_id_by_name` | Retorna o ID de um board pelo nome |
| `get_list_id_by_name` | Retorna o ID de uma coluna pelo nome (requer ID do board) |
| `get_list_id_by_board_name_and_list_name` | Retorna o ID de uma coluna pelos nomes do board e da coluna |
| `create_card` | Cria um novo card em uma coluna |
| `update_card` | Atualiza nome e/ou descrição de um card |
| `move_card` | Move um card para outra coluna |
| `archive_card` | Arquiva um card |
| `create_checklist` | Cria um checklist em um card |
| `add_check_item` | Adiciona um item a um checklist |
| `get_card_checklists` | Lista todos os checklists de um card |

## Testando via Postman

Na raiz do repositório há uma collection e um environment prontos para importar no Postman:

- `TrelloMcpHttp.postman_collection.json` — requests pré-configurados para todas as operações
- `TrelloMcpHttp.postman_environment.json` — variáveis de ambiente (preencha `api_key` e `token` após importar)

Para usar: importe os dois arquivos no Postman (File → Import), selecione o environment `TrelloMcpHttp` e preencha suas credenciais Trello nas variáveis `api_key` e `token`.

---

O protocolo MCP sobre HTTP usa o padrão JSON-RPC 2.0. Todas as chamadas são feitas via `POST /mcp`.

### 1. Inicializar a sessão

Antes de chamar qualquer tool, é necessário enviar a mensagem de inicialização do protocolo MCP.

- **Método:** `POST`
- **URL:** `https://localhost:7138/mcp`
- **Headers:**
  ```
  Content-Type: application/json
  X-Trello-Api-Key: <sua-api-key>
  X-Trello-Token: <seu-token>
  ```
- **Body (raw JSON):**
  ```json
  {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {
        "name": "postman-test",
        "version": "1.0"
      }
    }
  }
  ```

Guarde o header `mcp-session-id` retornado na resposta — ele é necessário nas chamadas seguintes.

### 2. Listar boards (`get_boards`)

- **Método:** `POST`
- **URL:** `https://localhost:7138/mcp`
- **Headers:**
  ```
  Content-Type: application/json
  X-Trello-Api-Key: <sua-api-key>
  X-Trello-Token: <seu-token>
  mcp-session-id: <id-retornado-no-initialize>
  ```
- **Body (raw JSON):**
  ```json
  {
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "get_boards",
      "arguments": {}
    }
  }
  ```

### 3. Criar um card (`create_card`)

- **Método:** `POST`
- **URL:** `https://localhost:7138/mcp`
- **Headers:** (mesmos do passo 2)
- **Body (raw JSON):**
  ```json
  {
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "create_card",
      "arguments": {
        "list_id": "<id-da-coluna>",
        "name": "Meu card de teste",
        "description": "Criado via Postman",
        "due": "2026-07-15T12:00:00Z"
      }
    }
  }
  ```

### 4. Listar tools disponíveis

Para descobrir todas as tools expostas pelo servidor:

- **Body (raw JSON):**
  ```json
  {
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/list",
    "params": {}
  }
  ```

## Arquitetura

```
Program.cs          — bootstrap: registra serviços e monta o servidor MCP
TrelloOptions.cs    — POCO com ApiKey e Token mapeados do appsettings.json
TrelloClient.cs     — wrapper sobre HttpClient para a API REST do Trello
TrelloTools.cs      — tools MCP expostas via atributos [McpServerTool]
```

O `TrelloClient` resolve credenciais priorizando os headers `X-Trello-Api-Key` / `X-Trello-Token` da requisição atual, com fallback para `TrelloOptions`.

## Stack

- .NET 10 / ASP.NET Core 10
- `ModelContextProtocol.AspNetCore` 1.4.0
- Stateless — apenas proxy para a API REST do Trello
