using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

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

    [McpServerTool, Description("Busca um card do Trello pelo ID.")]
    public async Task<string> get_card(
        [Description("ID do card")] string card_id,
        CancellationToken ct = default)
    {
        var result = await trello.GetCardAsync(card_id, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Retorna o ID de um board do Trello buscando pelo nome (sem distinção de maiúsculas/minúsculas). Use apenas quando já tiver o nome do board e precisar exclusivamente do ID. Se o objetivo final for obter o ID de uma coluna, prefira get_list_id_by_board_name_and_list_name.")]
    public async Task<string> get_board_id_by_name(
        [Description("Nome do board no Trello")] string board_name,
        CancellationToken ct = default)
    {
        var boards = await trello.GetBoardsAsync(ct);
        var board = boards?.FirstOrDefault(b => string.Equals(b?["name"]?.GetValue<string>(), board_name, StringComparison.OrdinalIgnoreCase));
        return board?["id"]?.GetValue<string>() ?? $"Board '{board_name}' não encontrado.";
    }

    [McpServerTool, Description("Retorna o ID de uma coluna (lista) de um board do Trello buscando pelo nome da coluna (sem distinção de maiúsculas/minúsculas). Use apenas quando já tiver o ID do board. Se tiver apenas o nome do board, prefira get_list_id_by_board_name_and_list_name.")]
    public async Task<string> get_list_id_by_name(
        [Description("ID do board no Trello")] string board_id,
        [Description("Nome da coluna (lista) no Trello")] string list_name,
        CancellationToken ct = default)
    {
        var lists = await trello.GetListsAsync(board_id, ct);
        var list = lists?.FirstOrDefault(l => string.Equals(l?["name"]?.GetValue<string>(), list_name, StringComparison.OrdinalIgnoreCase));
        return list?["id"]?.GetValue<string>() ?? $"Coluna '{list_name}' não encontrada no board '{board_id}'.";
    }

    [McpServerTool, Description("Retorna o ID de uma coluna (lista) buscando pelo nome do board e pelo nome da coluna, sem precisar de nenhum ID. Prefira esta ferramenta sempre que o usuário informar apenas os nomes do board e da coluna (sem distinção de maiúsculas/minúsculas).")]
    public async Task<string> get_list_id_by_board_name_and_list_name(
        [Description("Nome do board no Trello")] string board_name,
        [Description("Nome da coluna (lista) no Trello")] string list_name,
        CancellationToken ct = default)
    {
        var boards = await trello.GetBoardsAsync(ct);
        var board = boards?.FirstOrDefault(b => string.Equals(b?["name"]?.GetValue<string>(), board_name, StringComparison.OrdinalIgnoreCase));
        if (board is null) return $"Board '{board_name}' não encontrado.";

        var boardId = board["id"]!.GetValue<string>();
        var lists = await trello.GetListsAsync(boardId, ct);
        var list = lists?.FirstOrDefault(l => string.Equals(l?["name"]?.GetValue<string>(), list_name, StringComparison.OrdinalIgnoreCase));
        return list?["id"]?.GetValue<string>() ?? $"Coluna '{list_name}' não encontrada no board '{board_name}'.";
    }

    [McpServerTool, Description("Cria um novo card em uma coluna (list) do Trello.")]
    public async Task<string> create_card(
        [Description("ID da list onde o card será criado")] string list_id,
        [Description("Nome do card")] string name,
        [Description("Descrição do card (opcional)")] string description = "",
        [Description("Data de entrega no formato ISO 8601, ex: 2026-07-01T12:00:00Z (opcional)")] string? due = null,
        CancellationToken ct = default)
    {
        var result = await trello.CreateCardAsync(list_id, name, description, due, ct);
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

    [McpServerTool, Description("Cria um checklist em um card do Trello.")]
    public async Task<string> create_checklist(
        [Description("ID do card onde o checklist será criado")] string card_id,
        [Description("Nome do checklist")] string name,
        CancellationToken ct = default)
    {
        var result = await trello.CreateChecklistAsync(card_id, name, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Adiciona um item a um checklist do Trello.")]
    public async Task<string> add_check_item(
        [Description("ID do checklist")] string checklist_id,
        [Description("Nome do item")] string name,
        [Description("Se o item já deve iniciar marcado como concluído (padrão: false)")] bool @checked = false,
        CancellationToken ct = default)
    {
        var result = await trello.AddCheckItemAsync(checklist_id, name, @checked, ct);
        return result?.ToJsonString() ?? "{}";
    }

    [McpServerTool, Description("Lista todos os checklists de um card do Trello, incluindo seus itens.")]
    public async Task<string> get_card_checklists(
        [Description("ID do card no Trello")] string card_id,
        CancellationToken ct = default)
    {
        var result = await trello.GetCardChecklistsAsync(card_id, ct);
        return result?.ToJsonString() ?? "[]";
    }
}
