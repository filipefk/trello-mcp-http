using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;
using static McpToolkit.McpToolsHelpers;

namespace McpToolkit;

[McpServerToolType]
public sealed class BoardTools(McpClient trello)
{
    private const string BoardParam = "Board: nome (sem distinção de maiúsculas/minúsculas), ID ou URL do board";

    private static readonly string[] LabelColors =
        ["green", "yellow", "orange", "red", "purple", "blue", "sky", "lime", "pink", "black"];

    [McpServerTool(Name = "list_boards", Title = "Listar boards", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista os boards abertos do usuário dono do token do Trello, com id, nome, URL e data da última atividade.")]
    public async Task<string> ListBoards(CancellationToken ct = default)
    {
        var boards = await trello.ListBoardsAsync(ct);
        return Json(ToJsonArray(boards.OfType<JsonObject>().Select(SummarizeBoard)), "[]");
    }

    [McpServerTool(Name = "list_lists", Title = "Listar colunas", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista as colunas (lists) abertas de um board, na ordem em que aparecem no board (campo pos).")]
    public async Task<string> ListLists(
        [Description(BoardParam)] string board,
        CancellationToken ct = default)
    {
        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        var lists = await trello.ListListsAsync(boardId, ct);

        return Json(new JsonObject
        {
            ["boardId"] = boardId,
            ["lists"] = ToJsonArray(lists.OfType<JsonObject>().Select(SummarizeList))
        });
    }

    [McpServerTool(Name = "list_labels", Title = "Listar labels", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista as etiquetas (labels) de um board, com id, nome e cor. Use os nomes retornados aqui nos parâmetros 'labels' de create_card e update_card.")]
    public async Task<string> ListLabels(
        [Description(BoardParam)] string board,
        CancellationToken ct = default)
    {
        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        var labels = await trello.ListLabelsAsync(boardId, ct);

        return Json(new JsonObject
        {
            ["boardId"] = boardId,
            ["labels"] = ToJsonArray(labels.OfType<JsonObject>().Select(SummarizeLabel))
        });
    }

    [McpServerTool(Name = "list_board_members", Title = "Listar membros do board", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista os membros de um board, com id, username e nome completo. Use os usernames retornados aqui nos parâmetros 'members' de create_card e update_card.")]
    public async Task<string> ListBoardMembers(
        [Description(BoardParam)] string board,
        CancellationToken ct = default)
    {
        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        var members = await trello.ListBoardMembersAsync(boardId, ct);

        return Json(new JsonObject
        {
            ["boardId"] = boardId,
            ["members"] = ToJsonArray(members.OfType<JsonObject>().Select(SummarizeMember))
        });
    }

    [McpServerTool(Name = "create_label", Title = "Criar label", Destructive = false, OpenWorld = false),
     Description("Cria uma etiqueta (label) em um board. Só é necessário quando a etiqueta ainda não existe: create_card e update_card resolvem etiquetas existentes pelo nome.")]
    public async Task<string> CreateLabel(
        [Description(BoardParam)] string board,
        [Description("Nome da etiqueta")] string name,
        [Description("Cor da etiqueta: green, yellow, orange, red, purple, blue, sky, lime, pink ou black. Omita para etiqueta sem cor.")] string? color = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new McpException("Informe o nome da etiqueta.");

        if (!string.IsNullOrWhiteSpace(color) && !LabelColors.Contains(color.Trim().ToLowerInvariant()))
            throw new McpException($"Cor '{color}' inválida. Use uma de: {string.Join(", ", LabelColors)}.");

        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        var created = await trello.CreateLabelAsync(boardId, name.Trim(), color?.Trim().ToLowerInvariant(), ct) as JsonObject;

        var summary = SummarizeLabel(created);
        summary["boardId"] = boardId;
        return Json(summary);
    }
}
