FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TrelloMcpHttp.slnx .
COPY src/Backend/McpToolkit/McpToolkit.csproj src/Backend/McpToolkit/
COPY src/Backend/GeraApiKey/GeraApiKey.csproj src/Backend/GeraApiKey/
COPY src/Backend/TrelloMcpHttp/TrelloMcpHttp.csproj src/Backend/TrelloMcpHttp/
RUN dotnet restore src/Backend/TrelloMcpHttp/TrelloMcpHttp.csproj

COPY src/ src/
RUN dotnet publish src/Backend/TrelloMcpHttp/TrelloMcpHttp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN useradd -m appuser \
    && chown -R appuser:appuser /app
USER appuser

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TrelloMcpHttp.dll"]

## docker build -t filipefk/trello-mcp:1.0 -t filipefk/trello-mcp:latest .
## docker push filipefk/trello-mcp:1.0
## docker push filipefk/trello-mcp:latest
