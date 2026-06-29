using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace TrelloMcpHttp;

public sealed class TrelloClient(HttpClient http, IOptions<TrelloOptions> options)
{
    private readonly TrelloOptions _opts = options.Value;

    private string Auth(string? extra = null)
    {
        var q = $"key={_opts.ApiKey}&token={_opts.Token}";
        return extra is null ? q : $"{q}&{extra}";
    }

    public Task<JsonArray?> GetBoardsAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"members/me/boards?fields=id,name,url&{Auth()}", ct);

    public Task<JsonArray?> GetListsAsync(string boardId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"boards/{boardId}/lists?fields=id,name&{Auth()}", ct);

    public Task<JsonArray?> GetCardsAsync(string listId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"lists/{listId}/cards?fields=id,name,desc,idList,url&{Auth()}", ct);

    public async Task<JsonObject?> CreateCardAsync(string idList, string name, string desc, CancellationToken ct = default)
    {
        var response = await http.PostAsync(
            $"cards?idList={Uri.EscapeDataString(idList)}&name={Uri.EscapeDataString(name)}&desc={Uri.EscapeDataString(desc)}&{Auth()}",
            content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public async Task<JsonObject?> UpdateCardAsync(string cardId, string? name, string? desc, CancellationToken ct = default)
    {
        var extra = "";
        if (name is not null) extra += $"&name={Uri.EscapeDataString(name)}";
        if (desc is not null) extra += $"&desc={Uri.EscapeDataString(desc)}";

        var response = await http.PutAsync($"cards/{cardId}?{Auth(extra.TrimStart('&'))}", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public async Task<JsonObject?> MoveCardAsync(string cardId, string idList, CancellationToken ct = default)
    {
        var response = await http.PutAsync(
            $"cards/{cardId}?idList={Uri.EscapeDataString(idList)}&{Auth()}",
            content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public async Task<JsonObject?> ArchiveCardAsync(string cardId, CancellationToken ct = default)
    {
        var response = await http.PutAsync($"cards/{cardId}?closed=true&{Auth()}", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }
}
