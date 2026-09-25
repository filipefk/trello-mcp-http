using ModelContextProtocol;
using System.Net;

namespace McpToolkit;

// Herda de McpException para que a mensagem (status + erro da API) chegue ao cliente MCP;
// o SDK esconde a mensagem de qualquer outro tipo de exceção lançada por um tool.
public sealed class TrelloApiException(HttpStatusCode statusCode, string body)
    : McpException($"Trello API {(int)statusCode}: {Describe(statusCode, body)}")
{
    private const int MaxBodyChars = 2000;

    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Body { get; } = body;

    // A API do Trello responde erro em texto puro ("invalid id", "unauthorized permission requested"),
    // não em JSON. O 429 chega sem corpo útil e merece a explicação do limite.
    private static string Describe(HttpStatusCode statusCode, string body)
    {
        var text = body.Trim();

        if (statusCode == HttpStatusCode.TooManyRequests)
            return ("limite de requisições do Trello atingido (300 req/10s por chave, 100 req/10s por token); "
                + $"aguarde alguns segundos e tente de novo. {text}").Trim();

        if (text.Length == 0)
            return "a API não devolveu detalhes do erro.";

        return text.Length <= MaxBodyChars ? text : text[..MaxBodyChars] + "... (truncado)";
    }
}
