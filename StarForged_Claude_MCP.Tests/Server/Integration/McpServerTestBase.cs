using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

[Collection("McpServer")]
public abstract class McpServerTestBase
{
    protected readonly TestFixture _fixture;
    protected readonly McpServer _server;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected McpServerTestBase(TestFixture fixture)
    {
        _fixture = fixture;
        _server = _fixture.Services.GetRequiredService<McpServer>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected DbInterface Db => _fixture.Services.GetRequiredService<DbInterface>();

    protected async Task<JsonRpcResponse> InvokeServerMethod(JsonRpcRequest request) =>
        await McpServerInvoker.HandleRequestAsync(_server, request);

    protected async Task ClearTestDocuments() => await Db.DeleteAllDocuments();

    protected async Task ClearTestBeats() => await Db.DeleteAllBeats();

    protected async Task<JsonRpcResponse> CallTool(string id, string name, Dictionary<string, object> arguments) =>
        await InvokeServerMethod(new JsonRpcRequest
        {
            Id = id,
            Method = "tools/call",
            Params = new CallToolParams { Name = name, Arguments = arguments }
        });

    protected JsonElement ToolPayload(JsonRpcResponse response)
    {
        var result = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
        return JsonSerializer.Deserialize<JsonElement>(result!.Content[0].Text, _jsonOptions);
    }
}
