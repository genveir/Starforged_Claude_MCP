using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;
using System.Reflection;

namespace StarForged_Claude_MCP.Tests.Server;

internal static class McpServerInvoker
{
    public static async Task<JsonRpcResponse> HandleRequestAsync(McpServer server, JsonRpcRequest request)
    {
        var handleRequestMethod = typeof(McpServer)
            .GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        var task = (Task<JsonRpcResponse>)handleRequestMethod!.Invoke(server, [request])!;
        return await task;
    }
}
