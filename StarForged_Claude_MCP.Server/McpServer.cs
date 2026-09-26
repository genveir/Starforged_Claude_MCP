using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools;
using StarForged_Claude_MCP.Server.Tools.Abstractions;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server;

public class McpServer
{
    private readonly IReadOnlyList<ITool> _tools;
    private readonly Dictionary<string, ITool> _toolsByName = new();
    private readonly ILogger<McpServer> _logger;

    public McpServer(IEnumerable<ITool> tools, ILogger<McpServer> logger)
    {
        _tools = tools.ToList();
        _logger = logger;

        foreach (var tool in _tools)
        {
            if (!_toolsByName.TryAdd(tool.Definition.Name, tool))
                throw new InvalidOperationException($"More than one tool is registered under the name '{tool.Definition.Name}'.");
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await Console.In.ReadLineAsync(cancellationToken);
            if (line == null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, McpJson.Options);
                if (request == null) continue;

                // Notifications have no id and must not receive a response
                if (request.Id == null) continue;

                _logger.LogDebug("Received request: {Method} (id={Id})", request.Method, request.Id);
                var response = await HandleRequestAsync(request);
                var responseJson = McpJson.Serialize(response);
                await Console.Out.WriteLineAsync(responseJson);
                await Console.Out.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing request");
                var errorResponse = new JsonRpcResponse
                {
                    Id = 0,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Internal error",
                        Data = ex.Message
                    }
                };
                var errorJson = McpJson.Serialize(errorResponse);
                await Console.Out.WriteLineAsync(errorJson);
                await Console.Out.FlushAsync();
            }
        }
    }

    private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCallAsync(request),
            _ => new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                }
            }
        };
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        _logger.LogDebug("Handling initialize");
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new InitializeResult()
        };
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        _logger.LogDebug("Handling tools/list");
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new ToolsListResult { Tools = _tools.Select(tool => tool.Definition).ToList() }
        };
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        try
        {
            var paramsJson = McpJson.Serialize(request.Params);
            var callParams = JsonSerializer.Deserialize<CallToolParams>(paramsJson, McpJson.Options);

            if (callParams == null)
            {
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError { Code = -32602, Message = "Invalid params" }
                };
            }

            var resultText = await ExecuteToolAsync(callParams.Name, callParams.Arguments ?? new Dictionary<string, object>());

            _logger.LogInformation("Tool '{ToolName}' executed successfully", callParams.Name);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Text = resultText }
                    }
                }
            };
        }
        catch (ArgumentException ex)
        {
            // The tool ran and refused, which is something the caller can do something about: which
            // section names exist, that the category is still read-only. That has to travel in the
            // result, because a JSON-RPC error reaches the client as a protocol failure and the model
            // is shown whatever generic wording the client keeps for those.
            _logger.LogWarning("Tool call refused: {Message}", ex.Message);
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new CallToolResult
                {
                    IsError = true,
                    Content = new List<ToolContent>
                    {
                        new() { Text = ex.Message }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed");
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = "Tool execution failed",
                    Data = ex.Message
                }
            };
        }
    }

    private async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments)
    {
        if (!_toolsByName.TryGetValue(toolName, out var tool))
            throw new InvalidOperationException($"Unknown tool: {toolName}");

        return await tool.ExecuteAsync(new ToolArguments(arguments));
    }
}
