using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace TrelloMcpHttp;

public sealed class TrelloClient(HttpClient http, IOptions<TrelloOptions> options, IHttpContextAccessor httpContextAccessor)
{
    private readonly TrelloOptions _opts = options.Value;

    private string Auth(string? extra = null)
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        var apiKey = headers?["X-Trello-Api-Key"].FirstOrDefault() ?? _opts.ApiKey;
        var token = headers?["X-Trello-Token"].FirstOrDefault() ?? _opts.Token;
        var q = $"key={apiKey}&token={token}";
        return extra is null ? q : $"{q}&{extra}";
    }

    public Task<JsonArray?> GetBoardsAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"members/me/boards?fields=id,name,url&{Auth()}", ct);

    public Task<JsonArray?> GetListsAsync(string boardId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"boards/{boardId}/lists?fields=id,name&{Auth()}", ct);

    public Task<JsonArray?> GetCardsAsync(string listId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"lists/{listId}/cards?fields=id,name,desc,idList,url&{Auth()}", ct);

    public Task<JsonObject?> GetCardAsync(string cardId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonObject>($"cards/{cardId}?fields=id,name,desc,due,idList,url&{Auth()}", ct);

    public async Task<JsonObject?> CreateCardAsync(string idList, string name, string desc, string? due, CancellationToken ct = default)
    {
        var extra = $"idList={Uri.EscapeDataString(idList)}&name={Uri.EscapeDataString(name)}&desc={Uri.EscapeDataString(desc)}";
        if (due is not null) extra += $"&due={Uri.EscapeDataString(due)}";
        var response = await http.PostAsync($"cards?{extra}&{Auth()}", content: null, ct);
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

    public async Task<JsonObject?> CreateChecklistAsync(string cardId, string name, CancellationToken ct = default)
    {
        var response = await http.PostAsync(
            $"checklists?idCard={Uri.EscapeDataString(cardId)}&name={Uri.EscapeDataString(name)}&{Auth()}",
            content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public async Task<JsonObject?> AddCheckItemAsync(string checklistId, string name, bool @checked, CancellationToken ct = default)
    {
        var state = @checked ? "complete" : "incomplete";
        var response = await http.PostAsync(
            $"checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(name)}&checked={state}&{Auth()}",
            content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public Task<JsonArray?> GetCardChecklistsAsync(string cardId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"cards/{cardId}/checklists?{Auth()}", ct);

    public Task<JsonArray?> GetBoardLabelsAsync(string boardId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<JsonArray>($"boards/{boardId}/labels?{Auth()}", ct);

    public async Task<JsonObject?> CreateLabelAsync(string boardId, string name, string? color, CancellationToken ct = default)
    {
        var extra = $"idBoard={Uri.EscapeDataString(boardId)}&name={Uri.EscapeDataString(name)}";
        if (color is not null) extra += $"&color={Uri.EscapeDataString(color)}";
        var response = await http.PostAsync($"labels?{extra}&{Auth()}", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(ct);
    }

    public async Task<JsonArray?> AddLabelToCardAsync(string cardId, string labelId, CancellationToken ct = default)
    {
        var response = await http.PostAsync(
            $"cards/{cardId}/idLabels?value={Uri.EscapeDataString(labelId)}&{Auth()}",
            content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonArray>(ct);
    }

    public async Task RemoveLabelFromCardAsync(string cardId, string labelId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"cards/{cardId}/idLabels/{labelId}?{Auth()}", ct);
        response.EnsureSuccessStatusCode();
    }
}
