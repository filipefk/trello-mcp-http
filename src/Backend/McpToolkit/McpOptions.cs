namespace McpToolkit;

public sealed class McpOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string BaseAddress { get; set; } = "https://api.trello.com/1/";
}
