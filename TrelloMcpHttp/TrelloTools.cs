using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TrelloMcpHttp;

[McpServerToolType]
public sealed class TrelloTools(TrelloClient trello)
{
    [McpServerTool, Description("Lista todos os boards do usuário autenticado no Trello.")]
    public async Task<string> get_boards(CancellationToken ct = default)
    {
        var result = await trello.GetBoardsAsync(ct);
        return result?.ToJsonString() ?? "[]";
    }

    [McpServerTool, Description("Lista as colunas (lists) de um board do Trello.")]
    public async Task<string> get_lists(
        [Description("ID do board")] string board_id,
        CancellationToken ct = default)
    {
        var result = await trello.GetListsAsync(board_id, ct);
        return result?.ToJsonString() ?? "[]";
    }

    [McpServerTool, Description("Lista os cards de uma coluna (list) do Trello.")]
    public async Task<string> get_cards(
        [Description("ID da list")] string list_id,
        CancellationToken ct = default)
    {
        var result = await trello.GetCardsAsync(list_id, ct);
        return result?.ToJsonString() ?? "[]";
    }

    [McpServerTool, Description("Cria um novo card em uma coluna (list) do Trello.")]
    public async Task<string> create_card(
        [Description("ID da list onde o card será criado")] string list_id,
        [Description("Nome do card")] string name,
        [Description("Descrição do card (opcional)")] string description = "",
        CancellationToken ct = default)
    {
        var result = await trello.CreateCardAsync(list_id, name, description, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Atualiza o nome e/ou descrição de um card existente no Trello.")]
    public async Task<string> update_card(
        [Description("ID do card")] string card_id,
        [Description("Novo nome do card (null para não alterar)")] string? name = null,
        [Description("Nova descrição do card (null para não alterar)")] string? description = null,
        CancellationToken ct = default)
    {
        var result = await trello.UpdateCardAsync(card_id, name, description, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Move um card para outra coluna (list) do Trello.")]
    public async Task<string> move_card(
        [Description("ID do card")] string card_id,
        [Description("ID da list de destino")] string target_list_id,
        CancellationToken ct = default)
    {
        var result = await trello.MoveCardAsync(card_id, target_list_id, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Arquiva um card do Trello (equivale a fechá-lo).")]
    public async Task<string> archive_card(
        [Description("ID do card a arquivar")] string card_id,
        CancellationToken ct = default)
    {
        var result = await trello.ArchiveCardAsync(card_id, ct);
        return result?.ToJsonString() ?? "{}";
    }
}
