using Serilog;
using Serilog.Events;
using TrelloMcpHttp;
using TrelloMcpHttp.Middleware;

var builder = WebApplication.CreateBuilder(args);

var strLogLevel = builder.Configuration.GetValue<string>("Logging:LogLevel:Default");
var enumLogEventLevel = Enum.TryParse<LogEventLevel>(strLogLevel, true, out var parsedLogEventLevel)
    ? parsedLogEventLevel
    : LogEventLevel.Information;
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .WriteTo.Console(enumLogEventLevel)
        .WriteTo.File("logs/log.txt",
            enumLogEventLevel,
            rollingInterval: RollingInterval.Day));

builder.Services.Configure<TrelloOptions>(builder.Configuration.GetSection("Trello"));
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<TrelloClient>(client =>
{
    client.BaseAddress = new Uri("https://api.trello.com/1/");
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<TrelloTools>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    appBuilder => appBuilder.UseMiddleware<McpTrafficLoggingMiddleware>());

app.MapMcp("/mcp");

app.Run();
