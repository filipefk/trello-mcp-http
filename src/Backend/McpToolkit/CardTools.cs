using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;
using static McpToolkit.McpToolsHelpers;

namespace McpToolkit;

[McpServerToolType]
public sealed class CardTools(McpClient trello)
{
    private const string CardParam = "Card: ID, shortLink ou URL (https://trello.com/c/...)";
    private const string BoardParam = "Board: nome (sem distinção de maiúsculas/minúsculas), ID ou URL do board";
    private const string LabelsParam = "Etiquetas separadas por vírgula — nome, cor ou ID (ex: 'Bug, backend'). Veja list_labels.";
    private const string MembersParam = "Membros separados por vírgula — username, nome completo ou ID (ex: 'filipefk'). Veja list_board_members.";
    private const string PositionParam = "Posição na coluna: 'top', 'bottom' ou um número";
    private const string DueParam = "Data de entrega em YYYY-MM-DD ou ISO 8601 (ex: 2026-07-01T12:00:00Z)";

    [McpServerTool(Name = "get_card", Title = "Obter card", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Obtém um card do Trello pelo ID, shortLink ou URL. Retorna um resumo: nome, descrição, board, coluna, etiquetas, membros, data de entrega, posição e URL. Use full=true para incluir também os checklists e o JSON cru do card.")]
    public async Task<string> GetCard(
        [Description(CardParam)] string card,
        [Description("Se true, inclui os checklists e todos os campos do card")] bool full = false,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var found = await trello.GetCardAsync(cardId, full, ct) as JsonObject;
        return Json(SummarizeCard(found, full));
    }

    [McpServerTool(Name = "list_cards", Title = "Listar cards", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista os cards abertos de uma coluna de um board. Sem a coluna, lista os cards de todo o board.")]
    public async Task<string> ListCards(
        [Description(BoardParam)] string board,
        [Description("Coluna (list): nome ou ID. Omita para listar os cards de todo o board.")] string? list = null,
        [Description("Quantidade máxima de cards (padrão 50, máximo 200)")] int limit = 50,
        CancellationToken ct = default)
    {
        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        string? listId = null;
        string? listName = null;

        JsonArray cards;
        if (string.IsNullOrWhiteSpace(list))
        {
            cards = await trello.ListCardsInBoardAsync(boardId, ct);
        }
        else
        {
            (listId, listName) = await ResolveListAsync(trello, boardId, list, ct);
            cards = await trello.ListCardsInListAsync(listId, ct);
        }

        var summaries = cards.OfType<JsonObject>().Take(Math.Clamp(limit, 1, MaxLimit)).Select(c => SummarizeCard(c));

        return Json(new JsonObject
        {
            ["boardId"] = boardId,
            ["listId"] = listId,
            ["list"] = listName,
            ["count"] = cards.Count,
            ["cards"] = ToJsonArray(summaries)
        });
    }

    [McpServerTool(Name = "search_cards", Title = "Buscar cards", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Busca cards por texto em todos os boards do usuário, ou num board específico. Aceita a sintaxe de busca do Trello (ex: 'login is:open', 'due:week').")]
    public async Task<string> SearchCards(
        [Description("Texto ou expressão de busca do Trello")] string query,
        [Description("Board onde buscar: nome, ID ou URL (opcional — sem ele, busca em todos os boards)")] string? board = null,
        [Description("Quantidade máxima de cards (padrão 20, máximo 200)")] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new McpException("Informe o texto da busca.");

        var boardId = string.IsNullOrWhiteSpace(board) ? null : await ResolveBoardIdAsync(trello, board, ct);
        var cards = await trello.SearchCardsAsync(query, boardId, Math.Clamp(limit, 1, MaxLimit), ct);

        return Json(new JsonObject
        {
            ["query"] = query,
            ["boardId"] = boardId,
            ["count"] = cards.Count,
            ["cards"] = ToJsonArray(cards.OfType<JsonObject>().Select(c => SummarizeCard(c)))
        });
    }

    [McpServerTool(Name = "create_card", Title = "Criar card", Destructive = false, OpenWorld = false),
     Description("Cria um card numa coluna de um board. Board e coluna podem ser informados por nome. Sem a coluna, o card vai para a primeira coluna do board. A descrição é gravada como Markdown (é o que o Trello renderiza).")]
    public async Task<string> CreateCard(
        [Description(BoardParam)] string board,
        [Description("Título do card")] string name,
        [Description("Coluna (list): nome ou ID. Omita para usar a primeira coluna do board.")] string? list = null,
        [Description("Descrição do card em Markdown (opcional)")] string? desc = null,
        [Description(DueParam + " (opcional)")] string? due = null,
        [Description(LabelsParam + " (opcional)")] string? labels = null,
        [Description(MembersParam + " (opcional)")] string? members = null,
        [Description(PositionParam + " (opcional, padrão 'bottom')")] string? position = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new McpException("Informe o título do card.");

        var boardId = await ResolveBoardIdAsync(trello, board, ct);
        var (listId, listName) = await ResolveListAsync(trello, boardId, list, ct);

        var body = new JsonObject
        {
            ["idList"] = listId,
            ["name"] = name
        };
        if (desc is not null) body["desc"] = desc;

        var normalizedDue = NormalizeDue(due);
        if (!string.IsNullOrEmpty(normalizedDue)) body["due"] = normalizedDue;

        var labelIds = await ResolveLabelIdsAsync(trello, boardId, labels, ct);
        if (labelIds.Count > 0) body["idLabels"] = labelIds;

        var memberIds = await ResolveMemberIdsAsync(trello, boardId, members, ct);
        if (memberIds.Count > 0) body["idMembers"] = memberIds;

        if (!string.IsNullOrWhiteSpace(position)) body["pos"] = position.Trim();

        var created = await trello.CreateCardAsync(body, ct) as JsonObject;

        var summary = SummarizeCard(created);
        if (listName is not null) summary["list"] = listName;
        return Json(summary);
    }

    [McpServerTool(Name = "update_card", Title = "Atualizar card", Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Atualiza um card existente. Só os parâmetros informados são alterados (omitido = não altera; string vazia = limpa o campo, inclusive removendo todas as etiquetas ou membros). Para trocar de coluna use move_card.")]
    public async Task<string> UpdateCard(
        [Description(CardParam)] string card,
        [Description("Novo título")] string? name = null,
        [Description("Nova descrição em Markdown")] string? desc = null,
        [Description("Nova " + DueParam + "; string vazia remove a data")] string? due = null,
        [Description("Marca a data de entrega como concluída")] bool? due_complete = null,
        [Description("Novas etiquetas — substitui todas as atuais. " + LabelsParam + " String vazia remove todas.")] string? labels = null,
        [Description("Novos membros — substitui todos os atuais. " + MembersParam + " String vazia remove todos.")] string? members = null,
        [Description("Nova " + PositionParam)] string? position = null,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var body = new JsonObject();

        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new McpException("O título do card não pode ficar vazio.");
            body["name"] = name;
        }
        if (desc is not null) body["desc"] = desc;

        var normalizedDue = NormalizeDue(due);
        if (normalizedDue is not null)
            body["due"] = normalizedDue.Length == 0 ? null : JsonValue.Create(normalizedDue);

        if (due_complete is not null) body["dueComplete"] = due_complete;
        if (!string.IsNullOrWhiteSpace(position)) body["pos"] = position.Trim();

        // Etiquetas e membros são resolvidos pelo board do card.
        if (labels is not null || members is not null)
        {
            var current = await trello.GetCardAsync(cardId, false, ct) as JsonObject;
            var boardId = GetString(current, "idBoard")
                ?? throw new McpException($"Não foi possível descobrir o board do card '{card}'.");

            if (labels is not null) body["idLabels"] = await ResolveLabelIdsAsync(trello, boardId, labels, ct);
            if (members is not null) body["idMembers"] = await ResolveMemberIdsAsync(trello, boardId, members, ct);
        }

        if (body.Count == 0)
            throw new McpException("Nenhum campo para atualizar foi informado.");

        var updated = await trello.UpdateCardAsync(cardId, body, ct) as JsonObject;
        return Json(SummarizeCard(updated));
    }

    [McpServerTool(Name = "move_card", Title = "Mover card", Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Move um card para outra coluna, opcionalmente em outro board. Ao mudar de board, o Trello descarta as etiquetas e os membros que não existem no board de destino.")]
    public async Task<string> MoveCard(
        [Description(CardParam)] string card,
        [Description("Coluna de destino: nome ou ID")] string list,
        [Description("Board de destino: nome, ID ou URL (opcional — sem ele, o card fica no board atual)")] string? board = null,
        [Description(PositionParam + " (opcional)")] string? position = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(list))
            throw new McpException("Informe a coluna de destino.");

        var cardId = ResolveCardId(card);

        string boardId;
        var movingBoard = !string.IsNullOrWhiteSpace(board);
        if (movingBoard)
        {
            boardId = await ResolveBoardIdAsync(trello, board, ct);
        }
        else
        {
            var current = await trello.GetCardAsync(cardId, false, ct) as JsonObject;
            boardId = GetString(current, "idBoard")
                ?? throw new McpException($"Não foi possível descobrir o board do card '{card}'.");
        }

        var (listId, _) = await ResolveListAsync(trello, boardId, list, ct);

        var body = new JsonObject { ["idList"] = listId };
        if (movingBoard) body["idBoard"] = boardId;
        if (!string.IsNullOrWhiteSpace(position)) body["pos"] = position.Trim();

        var moved = await trello.UpdateCardAsync(cardId, body, ct) as JsonObject;
        return Json(SummarizeCard(moved));
    }

    [McpServerTool(Name = "archive_card", Title = "Arquivar card", Idempotent = true, OpenWorld = false),
     Description("Arquiva um card (equivale a fechá-lo no Trello). Use archived=false para desarquivar. O card não é apagado.")]
    public async Task<string> ArchiveCard(
        [Description(CardParam)] string card,
        [Description("true arquiva (padrão), false desarquiva")] bool archived = true,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var updated = await trello.UpdateCardAsync(cardId, new JsonObject { ["closed"] = archived }, ct) as JsonObject;
        return Json(SummarizeCard(updated));
    }

    [McpServerTool(Name = "add_comment", Title = "Adicionar comentário", Destructive = false, OpenWorld = false),
     Description("Adiciona um comentário na atividade de um card. O texto é renderizado como Markdown pelo Trello.")]
    public async Task<string> AddComment(
        [Description(CardParam)] string card,
        [Description("Texto do comentário em Markdown")] string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new McpException("Informe o texto do comentário.");

        var cardId = ResolveCardId(card);
        var created = await trello.AddCommentAsync(cardId, text, ct) as JsonObject;

        var summary = SummarizeComment(created);
        summary["cardId"] = cardId;
        return Json(summary);
    }

    [McpServerTool(Name = "list_comments", Title = "Listar comentários", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista os comentários de um card, do mais recente para o mais antigo, com texto, autor e data.")]
    public async Task<string> ListComments(
        [Description(CardParam)] string card,
        [Description("Quantidade máxima de comentários (padrão 20, máximo 200)")] int limit = 20,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var comments = await trello.ListCommentsAsync(cardId, Math.Clamp(limit, 1, MaxLimit), ct);

        return Json(new JsonObject
        {
            ["cardId"] = cardId,
            ["count"] = comments.Count,
            ["comments"] = ToJsonArray(comments.OfType<JsonObject>().Select(SummarizeComment))
        });
    }
}
