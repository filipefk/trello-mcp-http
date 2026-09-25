# CLAUDE.md

Instruções para o Claude Code ao trabalhar neste repositório.

## O que é este projeto

Servidor **MCP HTTP** em .NET 10 para manipular cards do **Trello**. Foi gerado a partir do template `ffkmcphttp` (`ModeloMcpHttp`) e segue a mesma arquitetura do projeto irmão `azure-cards-mcp-http` — se algo aqui parecer estranho, compare com ele antes de mudar.

Diferente do template, **este repositório é um MCP final**: `McpClient`, os tools e as opções são específicos do Trello e devem continuar assim.

Veja [README.md](README.md) para a documentação de uso (configuração, credenciais, registro no cliente MCP, lista de tools).

## Arquitetura

```
TrelloMcpHttp.slnx
└── src/Backend/
    ├── McpToolkit/        # class library (net10.0), sem dependência de ASP.NET
    ├── GeraApiKey/        # class library (net10.0), ofuscação da API Key
    └── TrelloMcpHttp/     # host ASP.NET Core (Microsoft.NET.Sdk.Web)
```

- **`McpToolkit/`** — tools MCP e cliente da API.
  - `McpClient.cs` — **único lugar** que fala com `https://api.trello.com/1/`. Autenticação por query string (`key`/`token`), corpo de POST/PUT em JSON UTF-8 (não query string) para não quebrar acentuação e Markdown em `name`/`desc`. Todo erro HTTP virá como `TrelloApiException`.
  - `BoardTools.cs` / `CardTools.cs` / `ChecklistTools.cs` — as três famílias de tools, cada uma ligável por `Tools:<Família>:Enabled`.
  - `McpToolsHelpers.cs` — resolução **nome ou ID** (board, coluna, etiqueta, membro), extração de ID de card a partir de URL/shortLink, normalização de data e os resumos de JSON devolvidos pelos tools. Toda lógica compartilhada entre tools mora aqui.
  - `McpOptions.cs` (`Trello:ApiKey`, `Trello:Token`, `Trello:BaseAddress`) e `IRequestContext.cs` (abstrai o header da requisição em curso, para a library não depender de `Microsoft.AspNetCore.Http`).
  - `Shell/` — tool de execução de comandos do SO herdada do template, em namespace próprio (`McpToolkit.Shell`) e **desligada por padrão** (`Shell:Enabled: false`).
- **`GeraApiKey/`** — `ApiKeyGenerator` junta uma lista de strings numa única string segura para header e desfaz. Cópia literal do projeto irmão; **não conhece o Trello** e não deve conhecer.
- **Raiz** — `Dockerfile` (publica o host numa imagem `aspnet:10.0`, porta `8080`, usuário não-root; ao criar um novo `.csproj` referenciado pelo host, acrescente o `COPY` dele antes do `restore`) e `GenerateApiKey.postman_*.json` (chamadas a `/api-key`).
- **`TrelloMcpHttp/`** — host: `Program.cs` (DI, Serilog, registro dos tools, middlewares, `MapMcp("/mcp")`), `McpApiKey.cs` (o único lugar que conhece a ordem `[mcpApiKey, trelloApiKey, trelloToken]` do header), `Middleware/`, `Endpoints/ApiKeyEndpoints.cs` e `wwwroot/index.html` (página pública da raiz que gera/decodifica a key chamando `/api-key`; cópia da do projeto irmão com a explicação de credenciais trocada pela do Trello).

### Credenciais

Resolvidas **a cada requisição** pelo `McpClient`: header `x-api-key` (2º e 3º valores) → `Trello:ApiKey`/`Trello:Token` → variáveis `TRELLO_API_KEY`/`TRELLO_TOKEN` (as mesmas da skill `trello-card`, via `PostConfigure<McpOptions>` no `Program.cs`). Ao mexer nessa cadeia, mantenha as três origens e a mensagem de erro que cita todas elas.

`/mcp` e `/api-key` são protegidos pelo `McpApiKeyMiddleware`, que valida o `mcp_api_key` contra `McpAuth:ApiKeys` em tempo constante. **Lista vazia rejeita tudo (fail-closed)** — não troque esse comportamento por um fallback permissivo.

## Convenções observadas no código

- `Nullable` e `ImplicitUsings` habilitados em todos os `.csproj`.
- Namespaces file-scoped (`namespace X;`), sem chaves.
- Primary constructors para injeção de dependência (`class Foo(IBar bar)`), inclusive nos tools MCP.
- `sealed` por padrão nas classes; `record` para tipos de retorno imutáveis (`ShellCommandResult`, DTOs dos endpoints).
- Tools são classes `[McpServerToolType]` com métodos `[McpServerTool(Name = "snake_case", Title = ..., ReadOnly/Destructive/Idempotent/OpenWorld)]`, `[Description]` em pt-BR em cada parâmetro, retorno `Task<string>` com JSON e parâmetros em `snake_case`. Use `CardTools.cs` como referência ao adicionar tools.
- Exceções lançadas dentro de um tool devem ser `ModelContextProtocol.McpException` (ou `TrelloApiException`, que herda dela) — o SDK esconde a mensagem de qualquer outro tipo.
- Sem comentários `<summary>` de doc XML — não adicionar a menos que pedido explicitamente (regra global do usuário).
- Comentários explicam **por quê**, não o quê; em pt-BR, como no restante do código.

**Atenção com `ModelContextProtocol.Core` em class libraries comuns:** ao contrário de projetos `Sdk.Web`, uma library `Microsoft.NET.Sdk` normal **não** resolve `Microsoft.Extensions.Options`/`Microsoft.Extensions.Logging.Abstractions` de graça mesmo em `net10.0` — é preciso referenciar os pacotes explicitamente no `.csproj` (já feito em `McpToolkit.csproj`). Se um linter/organizador de usings remover essas diretivas achando que estão sobrando, o build quebra com `CS0246 (IOptions<>)`.

## Regras de negócio ficam fora daqui

O servidor é **mecânico** sobre a API do Trello. Convenções de card (tipo Bug/Story/Task e sua etiqueta, sufixo no título, template de descrição com história de usuário e critérios Dado/Quando/Então) são responsabilidade do cliente — a skill `growdev-board-plugin/skills/trello-card` e a `generate-card-trello`. Não embuta essas regras nos tools.

## Build

Nunca execute `dotnet build`/`dotnet run` automaticamente após editar código, nem inclua isso como etapa de um plano — regra global do usuário. Só compile quando pedido explicitamente. Quando pedido, a solução compila com:

```bash
dotnet build TrelloMcpHttp.slnx
```
