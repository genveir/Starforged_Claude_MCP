using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;
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

    protected async Task<JsonRpcResponse> InvokeServerMethod(JsonRpcRequest request)
    {
        var handleRequestMethod = typeof(McpServer)
            .GetMethod("HandleRequestAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task<JsonRpcResponse>)handleRequestMethod!.Invoke(_server, [request])!;
        return await task;
    }

    protected async Task ClearTestMemories()
    {
        var dbInterface = _fixture.Services.GetRequiredService<DbInterface>();
        await dbInterface.DeleteAllEmbeddings();

        var vectorCache = _fixture.Services.GetRequiredService<VectorCacheService>();
        await vectorCache.RefreshCache();
    }
}
