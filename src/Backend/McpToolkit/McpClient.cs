using Microsoft.Extensions.Options;
using ModelContextProtocol;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpToolkit;

public sealed class McpClient(HttpClient http, IOptions<McpOptions> options, IRequestContext requestContext)
{
    public const string CardFields = "id,name,desc,url,shortUrl,shortLink,idBoard,idList,idMembers,labels,due,dueComplete,closed,pos,dateLastActivity";

    private const string JsonMediaType = "application/json";
    private const string MemberFields = "members=true&member_fields=id,username,fullName";

    private readonly McpOptions _opts = options.Value;

    // Chave e token são resolvidos a cada requisição: header x-api-key, senão appsettings,
    // senão as variáveis de ambiente usadas pela skill trello-card.
    private (string Key, string Token) ResolveCredentials()
    {
        var key = requestContext.TrelloApiKey;
        if (string.IsNullOrWhiteSpace(key)) key = _opts.ApiKey;

        var token = requestContext.TrelloToken;
        if (string.IsNullOrWhiteSpace(token)) token = _opts.Token;

        if (string.IsNullOrWhiteSpace(key))
            throw new McpException("Chave de API do Trello não informada (2º valor do header x-api-key, appsettings Trello:ApiKey ou variável TRELLO_API_KEY).");
        if (string.IsNullOrWhiteSpace(token))
            throw new McpException("Token do Trello não informado (3º valor do header x-api-key, appsettings Trello:Token ou variável TRELLO_TOKEN).");

        return (key.Trim(), token.Trim());
    }

    // O Trello autentica por query string (key/token); o corpo vai em JSON UTF-8 para não
    // quebrar acentuação e Markdown em name/desc.
    private async Task<JsonNode?> SendAsync(HttpMethod method, string pathAndQuery, JsonNode? body = null, CancellationToken ct = default)
    {
        var (key, token) = ResolveCredentials();
        var separator = pathAndQuery.Contains('?') ? "&" : "?";
        var uri = new Uri($"{pathAndQuery}{separator}key={Uri.EscapeDataString(key)}&token={Uri.EscapeDataString(token)}", UriKind.Relative);

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, JsonMediaType);

        using var response = await http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new TrelloApiException(response.StatusCode, content);

        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            // Algumas respostas de sucesso (ex: DELETE) não vêm em JSON.
            return new JsonObject { ["result"] = content.Trim() };
        }
    }

    public static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var query = string.Join("&", parts);
        return query.Length == 0 ? string.Empty : $"?{query}";
    }

    private static JsonArray AsArray(JsonNode? node) => node as JsonArray ?? [];

    // ---------- Boards e colunas ----------

    public async Task<JsonArray> ListBoardsAsync(CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, "members/me/boards?filter=open&fields=id,name,url,shortLink,dateLastActivity", ct: ct));

    public async Task<JsonArray> ListListsAsync(string boardId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"boards/{boardId}/lists?filter=open&fields=id,name,pos", ct: ct));

    public async Task<JsonArray> ListLabelsAsync(string boardId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"boards/{boardId}/labels?fields=id,name,color&limit=1000", ct: ct));

    public async Task<JsonArray> ListBoardMembersAsync(string boardId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"boards/{boardId}/members?fields=id,username,fullName", ct: ct));

    public Task<JsonNode?> CreateLabelAsync(string boardId, string name, string? color, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "labels", new JsonObject
        {
            ["idBoard"] = boardId,
            ["name"] = name,
            ["color"] = string.IsNullOrWhiteSpace(color) ? null : JsonValue.Create(color)
        }, ct);

    // ---------- Cards ----------

    public Task<JsonNode?> GetCardAsync(string cardId, bool full = false, CancellationToken ct = default)
    {
        var fields = full ? "fields=all&checklists=all" : $"fields={CardFields}";
        return SendAsync(HttpMethod.Get, $"cards/{cardId}?{fields}&{MemberFields}&list=true&board=true&board_fields=id,name,url", ct: ct);
    }

    public async Task<JsonArray> ListCardsInListAsync(string listId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"lists/{listId}/cards?fields={CardFields}&{MemberFields}", ct: ct));

    public async Task<JsonArray> ListCardsInBoardAsync(string boardId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"boards/{boardId}/cards?fields={CardFields}&{MemberFields}", ct: ct));

    public async Task<JsonArray> SearchCardsAsync(string query, string? boardId, int limit, CancellationToken ct = default)
    {
        var search = BuildQuery(
            ("query", query),
            ("modelTypes", "cards"),
            ("card_fields", CardFields),
            ("cards_limit", limit.ToString()),
            ("idBoards", boardId));
        var result = await SendAsync(HttpMethod.Get, $"search{search}", ct: ct);
        return AsArray(result?["cards"]);
    }

    public Task<JsonNode?> CreateCardAsync(JsonObject body, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "cards", body, ct);

    public Task<JsonNode?> UpdateCardAsync(string cardId, JsonObject body, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Put, $"cards/{cardId}", body, ct);

    // ---------- Comentários ----------

    public Task<JsonNode?> AddCommentAsync(string cardId, string text, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"cards/{cardId}/actions/comments", new JsonObject { ["text"] = text }, ct);

    public async Task<JsonArray> ListCommentsAsync(string cardId, int limit, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"cards/{cardId}/actions?filter=commentCard&limit={limit}", ct: ct));

    // ---------- Checklists ----------

    public async Task<JsonArray> ListChecklistsAsync(string cardId, CancellationToken ct = default) =>
        AsArray(await SendAsync(HttpMethod.Get, $"cards/{cardId}/checklists?fields=id,name,pos,idCard&checkItem_fields=name,state,pos", ct: ct));

    public Task<JsonNode?> CreateChecklistAsync(string cardId, string name, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "checklists", new JsonObject { ["idCard"] = cardId, ["name"] = name }, ct);

    public Task<JsonNode?> AddCheckItemAsync(string checklistId, string name, bool isChecked, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"checklists/{checklistId}/checkItems",
            new JsonObject { ["name"] = name, ["checked"] = isChecked }, ct);

    // A troca de estado de um item é feita pela rota do card, não pela do checklist.
    public Task<JsonNode?> UpdateCheckItemAsync(string cardId, string checkItemId, string? state, string? name, CancellationToken ct = default)
    {
        var body = new JsonObject();
        if (!string.IsNullOrWhiteSpace(state)) body["state"] = state;
        if (!string.IsNullOrWhiteSpace(name)) body["name"] = name;
        return SendAsync(HttpMethod.Put, $"cards/{cardId}/checkItem/{checkItemId}", body, ct);
    }

    public Task<JsonNode?> DeleteCheckItemAsync(string checklistId, string checkItemId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"checklists/{checklistId}/checkItems/{checkItemId}", ct: ct);

    public Task<JsonNode?> DeleteChecklistAsync(string checklistId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"checklists/{checklistId}", ct: ct);
}
