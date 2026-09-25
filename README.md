# Trello MCP HTTP

Servidor **MCP (Model Context Protocol) via HTTP** em .NET 10 para manipular cards do **Trello**: ler, listar, buscar, criar, atualizar, mover, arquivar, comentar e gerenciar checklists — além de boards, colunas, etiquetas e membros.

Construído sobre o template `ModeloMcpHttp` com o SDK oficial `ModelContextProtocol.AspNetCore`, usando a [API REST do Trello](https://developer.atlassian.com/cloud/trello/rest/) (`https://api.trello.com/1/`).

Board, coluna, etiqueta e membro podem ser informados **por nome ou por ID** no mesmo parâmetro — o servidor resolve sem distinção de maiúsculas/minúsculas.

## Estrutura

```
TrelloMcpHttp.slnx
└── src/Backend/
    ├── McpToolkit/                     # tools MCP + cliente da API
    │   ├── McpClient.cs                 # chamadas REST ao Trello
    │   ├── BoardTools.cs                # boards, colunas, etiquetas, membros
    │   ├── CardTools.cs                 # ler / listar / buscar / criar / atualizar / mover / arquivar / comentar
    │   ├── ChecklistTools.cs            # checklists e itens
    │   ├── McpToolsHelpers.cs           # resolução nome-ou-ID, resumos, datas
    │   ├── McpOptions.cs / IRequestContext.cs / TrelloApiException.cs
    │   └── Shell/                       # execução de comandos do SO (desligado por padrão)
    ├── GeraApiKey/                     # gera/desfaz a API Key única (lista de strings ⇄ string)
    │   └── ApiKeyGenerator.cs
    └── TrelloMcpHttp/                  # host ASP.NET Core (Program.cs, middlewares, endpoints, appsettings)
        └── wwwroot/index.html           # página da raiz que gera/decodifica a API Key
```

## Rodando

```bash
dotnet run --project src/Backend/TrelloMcpHttp/TrelloMcpHttp.csproj
```

O endpoint MCP fica em `http://localhost:5250/mcp` (perfil `http` do `launchSettings.json`).

### Docker

O `Dockerfile` na raiz publica o host em Release numa imagem `aspnet:10.0`, rodando como usuário não-root e escutando na porta `8080`:

```bash
docker build -t trello-mcp .
```

```bash
docker run -p 8080:8080 -e McpAuth__ApiKeys__0=minha-chave-mcp trello-mcp
```

Sem `McpAuth__ApiKeys__*` a lista fica vazia e toda chamada a `/mcp` e `/api-key` volta `401`. As credenciais do Trello podem ir na key ou em `-e Trello__ApiKey=... -e Trello__Token=...` (ou `TRELLO_API_KEY`/`TRELLO_TOKEN`).

## Configuração

### Header `x-api-key`

Toda chamada a `/mcp` precisa de **um único header**, `x-api-key` — pensado para conectores que só aceitam um cabeçalho (Copilot Studio, Claude Web). Ele é uma única string que carrega três valores, nesta ordem:

1. `mcp_api_key` — chave de acesso ao MCP, validada contra `McpAuth:ApiKeys` do `appsettings.json`;
2. chave de API do Trello (opcional);
3. token do Trello (opcional).

Sem o header, com uma key que não decodifica, com `mcp_api_key` fora da lista ou com a lista **vazia**, a resposta é `401`. Os endpoints `/api-key` também exigem o header, mas nele vai a `mcp_api_key` **em texto puro** (a key gerada é o que eles produzem/desfazem).

A key é gerada pela biblioteca `GeraApiKey`: os valores são unidos por um caractere não digitável (U+001F), embaralhados (XOR com semente aleatória) e codificados em Base64Url — só `A-Z a-z 0-9 - _`, seguro para header. **É ofuscação, não criptografia**: quem tem a key recupera os valores (inclusive o token do Trello). Trate a key como um segredo.

#### Gerando e desfazendo a key

O jeito mais simples é a **página da raiz** do servidor (ex.: <http://localhost:5250/>, servida de `wwwroot/index.html`): informe a sua chave MCP, a chave de API e o token do Trello (a página explica como obtê-los e monta a URL de autorização do token a partir da chave e do nome do aplicativo) e copie a key gerada, junto com o comando `claude mcp add` pronto. A mesma página decodifica uma key existente. Ela é pública, mas só chama os endpoints abaixo, que continuam exigindo a chave MCP.

Dois endpoints protegidos: o header `x-api-key` leva a `mcp_api_key` em texto puro, conferida contra `McpAuth:ApiKeys` (fora da lista ou ausente → `401`).

```bash
curl -X POST http://localhost:5250/api-key/generate -H "x-api-key: minha-chave-mcp" -H "Content-Type: application/json" -d "{\"mcpApiKey\":\"minha-chave-mcp\",\"trelloApiKey\":\"<sua-api-key>\",\"trelloToken\":\"<seu-token>\"}"
```

```bash
curl -X POST http://localhost:5250/api-key/decode -H "x-api-key: minha-chave-mcp" -H "Content-Type: application/json" -d "{\"apiKey\":\"<key>\"}"
```

Na raiz do repositório há uma collection e um environment do Postman com essas duas chamadas: `GenerateApiKey.postman_collection.json` e `GenerateApiKey.postman_environment.json`. Preencha `mcpApiKey`, `trelloApiKey` e `trelloToken` no environment; o `generate` grava a key gerada na variável `apiKey`, que o `decode` consome.

`generate` recebe o DTO `{ "mcpApiKey": "...", "trelloApiKey": "...", "trelloToken": "..." }` e devolve `{ "apiKey": "..." }`; `decode` faz o inverso e devolve os três campos. Só `mcpApiKey` é obrigatório (sem ele, `400`): `trelloApiKey` e `trelloToken` omitidos ou vazios saem como `null` no `decode` e fazem o servidor usar as credenciais da configuração. Os mesmos valores geram keys diferentes a cada chamada (semente aleatória), todas válidas.

### Credenciais do Trello

O servidor resolve as credenciais **a cada requisição**, nesta ordem:

| Prioridade | Chave de API | Token |
|---|---|---|
| 1 | 2º valor do header `x-api-key` | 3º valor do header `x-api-key` |
| 2 | `Trello:ApiKey` (appsettings / env `Trello__ApiKey`) | `Trello:Token` (appsettings / env `Trello__Token`) |
| 3 | variável `TRELLO_API_KEY` | variável `TRELLO_TOKEN` |

Uma key gerada só com a chave MCP (ou com os valores do Trello vazios) usa as credenciais configuradas no servidor. O passo 3 usa as mesmas variáveis da skill `trello-card`.

**Chave de API** — criando um aplicativo no Trello:

1. Faça login no Trello e acesse <https://trello.com/power-ups/admin/>.
2. Clique no botão **"Novo"** para criar um novo aplicativo.
3. Preencha os campos solicitados — o campo "URL de conector Iframe" não é obrigatório.
4. Clique em **"Gerar nova chave de API"**.
5. O valor exibido no campo **"Chave de API"** é a chave de API.

**Token** — monte a URL abaixo substituindo os placeholders pelos dados do seu aplicativo:

```
https://trello.com/1/authorize?expiration=never&scope=read,write&response_type=token&name=[nome+do+app]&key=[Chave de API]
```

> No parâmetro `name`, substitua espaços por `+` (ex.: `Meu App` → `Meu+App`).

1. Com o Trello aberto no navegador, acesse a URL montada acima.
2. Leia as permissões solicitadas e clique em **"Permitir"**.
3. Copie o token exibido na página seguinte.

As credenciais são do usuário: qualquer board que ele possa ler/escrever funciona com o mesmo par. Evite gravá-las no `appsettings.json`; prefira variável de ambiente, user secrets ou o header.

### `appsettings.json`

```json
{
  "McpAuth": {
    "ApiKeys": [ "minha-chave-mcp" ]
  },
  "Trello": {
    "ApiKey": "",
    "Token": "",
    "BaseAddress": "https://api.trello.com/1/"
  },
  "Tools": {
    "Boards": { "Enabled": true },
    "Cards": { "Enabled": true },
    "Checklists": { "Enabled": true }
  },
  "Shell": { "Enabled": false }
}
```

- `McpAuth:ApiKeys` — chaves MCP aceitas (1º valor do `x-api-key` em `/mcp`, header inteiro em `/api-key`). Lista vazia rejeita todas as chamadas a `/mcp` e a `/api-key`. Via variável de ambiente: `McpAuth__ApiKeys__0`, `McpAuth__ApiKeys__1`...
- `Trello:BaseAddress` — só muda para apontar para um proxy da API.
- `Tools:*:Enabled` — desliga uma família inteira de tools (ela nem aparece no `tools/list`).
- `Shell:Enabled` — tools `execute_shell_command` / `get_shell_info` herdadas do template; desligadas por padrão. Se ligar, configure ao menos `Shell:Password` (veja as opções em `ShellOptions`).

## Registrando no Claude Code

Gere a key na página da raiz do servidor ou em `/api-key/generate` (com os três campos, ou só o `mcpApiKey` para usar as credenciais configuradas no servidor) e registre:

```bash
claude mcp add --transport http trello http://localhost:5250/mcp --header "x-api-key: <key>"
```

Em conectores web (Copilot Studio, Claude Web), cadastre o mesmo header `x-api-key` com a key gerada.

## Tools

Todo parâmetro de card (`card`) aceita o ID, o `shortLink` ou a URL do card (`https://trello.com/c/...`). Todo parâmetro `board`, `list`, `labels` e `members` aceita nome ou ID.

### Boards (`BoardTools`)

| Tool | O que faz |
|---|---|
| `list_boards` | Boards abertos do usuário dono do token. |
| `list_lists` | Colunas abertas de um board, na ordem do board. |
| `list_labels` | Etiquetas do board (id, nome, cor). |
| `list_board_members` | Membros do board (id, username, nome). |
| `create_label` | Cria uma etiqueta no board. Só é necessário quando ela ainda não existe. |

### Cards (`CardTools`)

| Tool | O que faz |
|---|---|
| `get_card` | Resumo do card: nome, descrição, board, coluna, etiquetas, membros, data de entrega, posição e URL. `full=true` inclui checklists e o JSON cru. |
| `list_cards` | Cards de uma coluna; sem a coluna, de todo o board. |
| `search_cards` | Busca por texto, em todos os boards ou num board específico. Aceita a sintaxe de busca do Trello. |
| `create_card` | Cria um card. Sem a coluna, usa a primeira do board. Aceita descrição (Markdown), data de entrega, etiquetas, membros e posição. |
| `update_card` | Altera título, descrição, data de entrega, `dueComplete`, etiquetas, membros e posição. Omitido = não altera; `""` = limpa. |
| `move_card` | Move para outra coluna e, opcionalmente, outro board. |
| `archive_card` | Arquiva (ou desarquiva, com `archived=false`). O card não é apagado. |
| `add_comment` | Adiciona um comentário (Markdown). |
| `list_comments` | Comentários do card, do mais recente para o mais antigo. |

A descrição do card é gravada como **Markdown**, que é o que o Trello renderiza. O servidor não impõe nenhum modelo de card: título, tipo e formato da descrição são responsabilidade de quem chama.

### Checklists (`ChecklistTools`)

| Tool | O que faz |
|---|---|
| `list_checklists` | Checklists do card com todos os itens e seus estados. |
| `create_checklist` | Cria o checklist já com os itens informados. |
| `add_checklist_items` | Acrescenta itens a um checklist existente. |
| `set_check_item_state` | Marca/desmarca um item e, opcionalmente, troca o texto. |
| `delete_check_item` | Exclui um item definitivamente. |
| `delete_checklist` | Exclui o checklist e seus itens definitivamente. |

O Trello aceita um item por chamada: `create_checklist` e `add_checklist_items` iteram internamente e, se um item falhar, os demais continuam e a falha sai em `errors`.

## Limites da API

O Trello limita a **300 requisições/10s por chave** e **100 requisições/10s por token**. Ao estourar, a resposta `429` chega ao cliente MCP como uma mensagem explicando o limite — não há retry automático.

## Logs

Serilog em console e `logs/log.txt` (rotação diária). O middleware de tráfego registra as chamadas `POST /mcp` com os headers `x-api-key` e `Authorization` e o campo `password` redigidos (`***`); a chave e o token do Trello viajam na query string montada pelo servidor e não são logados. Chamadas rejeitadas por falta de autorização são logadas como aviso com o motivo (sem a chave).

## Requisitos

- .NET 10 SDK
