using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;
using static McpToolkit.McpToolsHelpers;

namespace McpToolkit;

[McpServerToolType]
public sealed class ChecklistTools(McpClient trello)
{
    private const string CardParam = "Card: ID, shortLink ou URL (https://trello.com/c/...)";
    private const string ChecklistParam = "ID do checklist (veja list_checklists)";

    [McpServerTool(Name = "list_checklists", Title = "Listar checklists", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Lista os checklists de um card, com os itens de cada um (id, texto, estado 'complete' ou 'incomplete') na ordem em que aparecem.")]
    public async Task<string> ListChecklists(
        [Description(CardParam)] string card,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var checklists = await trello.ListChecklistsAsync(cardId, ct);

        return Json(new JsonObject
        {
            ["cardId"] = cardId,
            ["checklists"] = ToJsonArray(checklists.OfType<JsonObject>().Select(c => SummarizeChecklist(c)))
        });
    }

    [McpServerTool(Name = "create_checklist", Title = "Criar checklist", Destructive = false, OpenWorld = false),
     Description("Cria um checklist num card, já com os itens informados. Se algum item falhar, os demais continuam e a falha é listada em 'errors'.")]
    public async Task<string> CreateChecklist(
        [Description(CardParam)] string card,
        [Description("Nome do checklist")] string name,
        [Description("Itens do checklist, na ordem (opcional)")] string[]? items = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new McpException("Informe o nome do checklist.");

        var cardId = ResolveCardId(card);
        var created = await trello.CreateChecklistAsync(cardId, name, ct) as JsonObject;
        var checklistId = GetString(created, "id")
            ?? throw new McpException("O Trello não devolveu o ID do checklist criado.");

        var (addedItems, errors) = await AddItemsAsync(checklistId, items, ct);

        var summary = SummarizeChecklist(created);
        summary["cardId"] = cardId;
        summary["items"] = addedItems;
        summary["errors"] = errors;
        return Json(summary);
    }

    [McpServerTool(Name = "add_checklist_items", Title = "Adicionar itens ao checklist", Destructive = false, OpenWorld = false),
     Description("Adiciona itens ao final de um checklist existente. Se algum item falhar, os demais continuam e a falha é listada em 'errors'.")]
    public async Task<string> AddChecklistItems(
        [Description(ChecklistParam)] string checklist,
        [Description("Itens a adicionar, na ordem")] string[] items,
        [Description("Se true, os itens já entram marcados como concluídos (padrão false)")] bool complete = false,
        CancellationToken ct = default)
    {
        var checklistId = RequireObjectId(checklist, "checklist");
        if (items is null || items.Length == 0)
            throw new McpException("Informe ao menos um item.");

        var (addedItems, errors) = await AddItemsAsync(checklistId, items, ct, complete);

        return Json(new JsonObject
        {
            ["checklistId"] = checklistId,
            ["items"] = addedItems,
            ["errors"] = errors
        });
    }

    [McpServerTool(Name = "set_check_item_state", Title = "Marcar item do checklist", Destructive = false, Idempotent = true, OpenWorld = false),
     Description("Marca ou desmarca um item de checklist e, opcionalmente, troca o texto dele. O item é identificado pelo card e pelo ID do item (veja list_checklists).")]
    public async Task<string> SetCheckItemState(
        [Description(CardParam)] string card,
        [Description("ID do item do checklist (veja list_checklists)")] string check_item,
        [Description("true marca como concluído (padrão), false desmarca")] bool complete = true,
        [Description("Novo texto do item (opcional)")] string? new_name = null,
        CancellationToken ct = default)
    {
        var cardId = ResolveCardId(card);
        var checkItemId = RequireObjectId(check_item, "item de checklist");

        var updated = await trello.UpdateCheckItemAsync(cardId, checkItemId, complete ? "complete" : "incomplete", new_name, ct) as JsonObject;

        var summary = SummarizeCheckItem(updated);
        summary["cardId"] = cardId;
        return Json(summary);
    }

    [McpServerTool(Name = "delete_check_item", Title = "Excluir item do checklist", Destructive = true, Idempotent = true, OpenWorld = false),
     Description("Exclui definitivamente um item de um checklist. Não há como desfazer — para apenas desmarcar o item, use set_check_item_state.")]
    public async Task<string> DeleteCheckItem(
        [Description(ChecklistParam)] string checklist,
        [Description("ID do item do checklist (veja list_checklists)")] string check_item,
        CancellationToken ct = default)
    {
        var checklistId = RequireObjectId(checklist, "checklist");
        var checkItemId = RequireObjectId(check_item, "item de checklist");

        await trello.DeleteCheckItemAsync(checklistId, checkItemId, ct);

        return Json(new JsonObject
        {
            ["checklistId"] = checklistId,
            ["checkItemId"] = checkItemId,
            ["deleted"] = true
        });
    }

    [McpServerTool(Name = "delete_checklist", Title = "Excluir checklist", Destructive = true, Idempotent = true, OpenWorld = false),
     Description("Exclui definitivamente um checklist e todos os seus itens do card. Não há como desfazer.")]
    public async Task<string> DeleteChecklist(
        [Description(ChecklistParam)] string checklist,
        CancellationToken ct = default)
    {
        var checklistId = RequireObjectId(checklist, "checklist");
        await trello.DeleteChecklistAsync(checklistId, ct);

        return Json(new JsonObject
        {
            ["checklistId"] = checklistId,
            ["deleted"] = true
        });
    }

    // O Trello só aceita um item por chamada; a falha de um item não interrompe os demais.
    private async Task<(JsonArray Items, JsonArray Errors)> AddItemsAsync(
        string checklistId, string[]? items, CancellationToken ct, bool complete = false)
    {
        var added = new JsonArray();
        var errors = new JsonArray();

        foreach (var item in items ?? [])
        {
            if (string.IsNullOrWhiteSpace(item)) continue;

            try
            {
                var created = await trello.AddCheckItemAsync(checklistId, item, complete, ct) as JsonObject;
                added.Add(SummarizeCheckItem(created));
            }
            catch (Exception ex) when (ex is McpException or HttpRequestException)
            {
                errors.Add(new JsonObject { ["item"] = item, ["message"] = ex.Message });
            }
        }

        return (added, errors);
    }
}
