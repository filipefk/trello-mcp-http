using ModelContextProtocol;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace McpToolkit;

// Helpers compartilhados entre as classes de tools MCP (BoardTools, CardTools, ChecklistTools).
// A resolução "nome ou ID" vive aqui: todo parâmetro de board, coluna, label e membro aceita
// o ID do Trello ou o nome, resolvido sem distinção de maiúsculas/minúsculas.
internal static partial class McpToolsHelpers
{
    public const int MaxLimit = 200;

    private static readonly char[] ListSeparators = [',', ';', '\n'];

    [GeneratedRegex("^[0-9a-f]{24}$", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectIdRegex();

    [GeneratedRegex("^[A-Za-z0-9]{8}$")]
    private static partial Regex ShortLinkRegex();

    [GeneratedRegex(@"trello\.com/c/([A-Za-z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CardUrlRegex();

    [GeneratedRegex(@"trello\.com/b/([A-Za-z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoardUrlRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateOnlyRegex();

    public static string Json(JsonNode? node, string fallback = "{}") => node?.ToJsonString() ?? fallback;

    public static JsonArray ToJsonArray(IEnumerable<JsonObject> items) => new([.. items.Select(i => (JsonNode)i)]);

    public static string? GetString(JsonObject? obj, string name) =>
        obj?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double GetDouble(JsonObject? obj, string name) =>
        obj?[name] is JsonValue value && value.TryGetValue<double>(out var number) ? number : double.MaxValue;

    private static string Quote(string? value) => $"'{value}'";

    // ---------- IDs ----------

    public static bool IsObjectId(string? value) => value is not null && ObjectIdRegex().IsMatch(value.Trim());

    // Aceita o ID de 24 caracteres, o shortLink de 8 (que a API também aceita no lugar do ID)
    // ou a URL do card (https://trello.com/c/<shortLink>/...).
    public static string ResolveCardId(string cardIdOrUrl)
    {
        var value = (cardIdOrUrl ?? string.Empty).Trim();

        var match = CardUrlRegex().Match(value);
        if (match.Success) return match.Groups[1].Value;

        if (ObjectIdRegex().IsMatch(value) || ShortLinkRegex().IsMatch(value)) return value;

        throw new McpException($"ID de card inválido: {Quote(cardIdOrUrl)}. Informe o ID, o shortLink ou a URL do card.");
    }

    public static string RequireObjectId(string value, string what)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (ObjectIdRegex().IsMatch(trimmed)) return trimmed;

        throw new McpException($"ID de {what} inválido: {Quote(value)}. Informe o ID de 24 caracteres devolvido pela API.");
    }

    // ---------- Board, coluna, label e membro por nome ou ID ----------

    public static async Task<string> ResolveBoardIdAsync(McpClient client, string? boardNameOrId, CancellationToken ct)
    {
        var value = (boardNameOrId ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new McpException("Informe o board (nome, ID ou URL).");

        var urlMatch = BoardUrlRegex().Match(value);
        if (urlMatch.Success) return urlMatch.Groups[1].Value;

        if (ObjectIdRegex().IsMatch(value) || ShortLinkRegex().IsMatch(value)) return value;

        var boards = (await client.ListBoardsAsync(ct)).OfType<JsonObject>().ToList();
        var matches = boards
            .Where(b => string.Equals(GetString(b, "name"), value, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1) return GetString(matches[0], "id")!;
        if (matches.Count > 1)
            throw new McpException($"Mais de um board chamado {Quote(value)}. Informe o ID do board (veja list_boards).");

        var names = string.Join(", ", boards.Select(b => GetString(b, "name")).Where(n => !string.IsNullOrWhiteSpace(n)));
        throw new McpException($"Board {Quote(value)} não encontrado. Boards disponíveis: {(names.Length == 0 ? "nenhum" : names)}.");
    }

    // Coluna omitida resolve para a primeira do board (menor pos).
    public static async Task<(string Id, string? Name)> ResolveListAsync(McpClient client, string boardId, string? listNameOrId, CancellationToken ct)
    {
        var value = (listNameOrId ?? string.Empty).Trim();
        if (ObjectIdRegex().IsMatch(value)) return (value, null);

        var lists = (await client.ListListsAsync(boardId, ct)).OfType<JsonObject>().ToList();

        if (value.Length == 0)
        {
            var first = lists.OrderBy(l => GetDouble(l, "pos")).FirstOrDefault()
                ?? throw new McpException("O board não tem nenhuma coluna aberta.");
            return (GetString(first, "id")!, GetString(first, "name"));
        }

        var matches = lists
            .Where(l => string.Equals(GetString(l, "name"), value, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1) return (GetString(matches[0], "id")!, GetString(matches[0], "name"));
        if (matches.Count > 1)
            throw new McpException($"Mais de uma coluna chamada {Quote(value)} neste board. Informe o ID da coluna (veja list_lists).");

        var names = string.Join(", ", lists.Select(l => GetString(l, "name")).Where(n => !string.IsNullOrWhiteSpace(n)));
        throw new McpException($"Coluna {Quote(value)} não encontrada no board. Colunas: {(names.Length == 0 ? "nenhuma" : names)}.");
    }

    public static IReadOnlyList<string> SplitValues(string? values) =>
        [.. (values ?? string.Empty).Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    // Labels casam por ID, nome ou cor. Label inexistente é erro (para criar, use create_label).
    public static async Task<JsonArray> ResolveLabelIdsAsync(McpClient client, string boardId, string? labels, CancellationToken ct)
    {
        var wanted = SplitValues(labels);
        if (wanted.Count == 0) return [];

        var boardLabels = (await client.ListLabelsAsync(boardId, ct)).OfType<JsonObject>().ToList();
        var ids = new JsonArray();

        foreach (var value in wanted)
        {
            if (ObjectIdRegex().IsMatch(value))
            {
                ids.Add(value);
                continue;
            }

            var match = boardLabels.FirstOrDefault(l => string.Equals(GetString(l, "name"), value, StringComparison.OrdinalIgnoreCase))
                ?? boardLabels.FirstOrDefault(l => string.Equals(GetString(l, "color"), value, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpException($"Label {Quote(value)} não existe neste board (veja list_labels ou crie com create_label).");

            ids.Add(GetString(match, "id")!);
        }

        return ids;
    }

    public static async Task<JsonArray> ResolveMemberIdsAsync(McpClient client, string boardId, string? members, CancellationToken ct)
    {
        var wanted = SplitValues(members);
        if (wanted.Count == 0) return [];

        var boardMembers = (await client.ListBoardMembersAsync(boardId, ct)).OfType<JsonObject>().ToList();
        var ids = new JsonArray();

        foreach (var value in wanted)
        {
            if (ObjectIdRegex().IsMatch(value))
            {
                ids.Add(value);
                continue;
            }

            var match = boardMembers.FirstOrDefault(m => string.Equals(GetString(m, "username"), value.TrimStart('@'), StringComparison.OrdinalIgnoreCase))
                ?? boardMembers.FirstOrDefault(m => string.Equals(GetString(m, "fullName"), value, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpException($"Membro {Quote(value)} não está neste board (veja list_board_members).");

            ids.Add(GetString(match, "id")!);
        }

        return ids;
    }

    // ---------- Datas ----------

    // Aceita YYYY-MM-DD (vira meia-noite UTC) ou ISO 8601 completo. String vazia limpa a data no card.
    public static string? NormalizeDue(string? due)
    {
        if (due is null) return null;

        var value = due.Trim();
        if (value.Length == 0) return string.Empty;
        if (DateOnlyRegex().IsMatch(value)) return $"{value}T00:00:00Z";

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            throw new McpException($"Data inválida: {Quote(due)}. Use YYYY-MM-DD ou ISO 8601 (ex: 2026-07-01T12:00:00Z).");

        return parsed.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    // ---------- Resumos ----------

    public static JsonObject SummarizeBoard(JsonObject? board) => new()
    {
        ["id"] = GetString(board, "id"),
        ["name"] = GetString(board, "name"),
        ["url"] = GetString(board, "url"),
        ["shortLink"] = GetString(board, "shortLink"),
        ["dateLastActivity"] = board?["dateLastActivity"]?.DeepClone()
    };

    public static JsonObject SummarizeList(JsonObject? list) => new()
    {
        ["id"] = GetString(list, "id"),
        ["name"] = GetString(list, "name"),
        ["pos"] = list?["pos"]?.DeepClone()
    };

    public static JsonObject SummarizeLabel(JsonObject? label) => new()
    {
        ["id"] = GetString(label, "id"),
        ["name"] = GetString(label, "name"),
        ["color"] = GetString(label, "color")
    };

    public static JsonObject SummarizeMember(JsonObject? member) => new()
    {
        ["id"] = GetString(member, "id"),
        ["username"] = GetString(member, "username"),
        ["fullName"] = GetString(member, "fullName")
    };

    public static JsonObject SummarizeCard(JsonObject? card, bool full = false)
    {
        var summary = new JsonObject
        {
            ["id"] = GetString(card, "id"),
            ["name"] = GetString(card, "name"),
            ["url"] = GetString(card, "shortUrl") ?? GetString(card, "url"),
            ["shortLink"] = GetString(card, "shortLink"),
            ["boardId"] = GetString(card, "idBoard"),
            ["board"] = GetString(card?["board"] as JsonObject, "name"),
            ["listId"] = GetString(card, "idList"),
            ["list"] = GetString(card?["list"] as JsonObject, "name"),
            ["labels"] = ToJsonArray((card?["labels"] as JsonArray ?? []).OfType<JsonObject>().Select(SummarizeLabel)),
            ["members"] = ToJsonArray((card?["members"] as JsonArray ?? []).OfType<JsonObject>().Select(SummarizeMember)),
            ["due"] = card?["due"]?.DeepClone(),
            ["dueComplete"] = card?["dueComplete"]?.DeepClone(),
            ["closed"] = card?["closed"]?.DeepClone(),
            ["pos"] = card?["pos"]?.DeepClone(),
            ["dateLastActivity"] = card?["dateLastActivity"]?.DeepClone(),
            ["desc"] = GetString(card, "desc")
        };

        if (full)
        {
            summary["checklists"] = ToJsonArray((card?["checklists"] as JsonArray ?? []).OfType<JsonObject>().Select(c => SummarizeChecklist(c)));
            summary["raw"] = card?.DeepClone();
        }

        return summary;
    }

    public static JsonObject SummarizeChecklist(JsonObject? checklist) => new()
    {
        ["id"] = GetString(checklist, "id"),
        ["name"] = GetString(checklist, "name"),
        ["cardId"] = GetString(checklist, "idCard"),
        ["pos"] = checklist?["pos"]?.DeepClone(),
        ["items"] = ToJsonArray((checklist?["checkItems"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .OrderBy(i => GetDouble(i, "pos"))
            .Select(SummarizeCheckItem))
    };

    public static JsonObject SummarizeCheckItem(JsonObject? item) => new()
    {
        ["id"] = GetString(item, "id"),
        ["name"] = GetString(item, "name"),
        ["state"] = GetString(item, "state"),
        ["pos"] = item?["pos"]?.DeepClone()
    };

    // Comentários chegam como ações do card: o texto fica em data.text e o autor em memberCreator.
    public static JsonObject SummarizeComment(JsonObject? action) => new()
    {
        ["id"] = GetString(action, "id"),
        ["text"] = GetString(action?["data"] as JsonObject, "text"),
        ["createdBy"] = FormatMember(action?["memberCreator"] as JsonObject),
        ["date"] = action?["date"]?.DeepClone()
    };

    private static string? FormatMember(JsonObject? member)
    {
        var fullName = GetString(member, "fullName");
        var username = GetString(member, "username");

        if (string.IsNullOrWhiteSpace(username)) return fullName;
        return string.IsNullOrWhiteSpace(fullName) ? $"@{username}" : $"{fullName} (@{username})";
    }
}
