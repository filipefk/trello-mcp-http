using TrelloMcpHttp;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

app.MapMcp();

app.Run();
