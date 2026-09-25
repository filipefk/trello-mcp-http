using McpToolkit;

namespace TrelloMcpHttp;

// Decodifica o header x-api-key da requisição em curso (já validado pelo McpApiKeyMiddleware).
public sealed class HttpRequestContext(IHttpContextAccessor httpContextAccessor) : IRequestContext
{
    public string? TrelloApiKey => Read()?.TrelloApiKey;

    public string? TrelloToken => Read()?.TrelloToken;

    private McpApiKey? Read() =>
        McpApiKey.TryRead(httpContextAccessor.HttpContext, out var apiKey) ? apiKey : null;
}
