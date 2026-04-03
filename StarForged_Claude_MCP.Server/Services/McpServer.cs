using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StarForged_Claude_MCP.Server.Services;

public class McpServer
{
    private readonly EmbeddingsFacade _embeddings;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(EmbeddingsFacade backend)
    {
        _embeddings = backend;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
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
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null) continue;

                var response = await HandleRequestAsync(request);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                await Console.Out.WriteLineAsync(responseJson);
                await Console.Out.FlushAsync();
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonRpcResponse
                {
                    Id = null,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Internal error",
                        Data = ex.Message
                    }
                };
                var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
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
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new InitializeResult()
        };
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new List<Tool>
        {
            new()
            {
                Name = "search",
                Description = "Search for relevant campaign information using semantic similarity",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language search query" },
                        topK = new { type = "number", description = "Number of results to return (default: 3, max: 10)" }
                    },
                    required = new[] { "query" }
                }
            },
            new()
            {
                Name = "add_memory",
                Description = "Store a new fact, memory, or campaign detail",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "The content to store" },
                        sourceDocument = new { type = "string", description = "Category or identifier (e.g., 'campaign_session_5')" }
                    },
                    required = new[] { "text", "sourceDocument" }
                }
            },
            new()
            {
                Name = "delete_memory",
                Description = "Delete a stored memory by ID",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "number", description = "The ID of the memory to delete" }
                    },
                    required = new[] { "id" }
                }
            },
            new()
            {
                Name = "get_memory",
                Description = "Retrieve a specific memory by ID",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "number", description = "The ID of the memory to retrieve" }
                    },
                    required = new[] { "id" }
                }
            },
            new()
            {
                Name = "upload_file",
                Description = "Upload and chunk a markdown document into searchable sections",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "Markdown content to chunk and store" },
                        sourceDocument = new { type = "string", description = "Document identifier (e.g., 'world_lore_v2')" }
                    },
                    required = new[] { "text", "sourceDocument" }
                }
            }
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new ToolsListResult { Tools = tools }
        };
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
    {
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var callParams = JsonSerializer.Deserialize<CallToolParams>(paramsJson, _jsonOptions);

            if (callParams == null || callParams.Arguments == null)
            {
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError { Code = -32602, Message = "Invalid params" }
                };
            }

            var resultText = await ExecuteToolAsync(callParams.Name, callParams.Arguments);

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
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32602,
                    Message = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
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
        return toolName switch
        {
            "search" => await ExecuteSearchAsync(arguments),
            "add_memory" => await ExecuteAddMemoryAsync(arguments),
            "delete_memory" => await ExecuteDeleteMemoryAsync(arguments),
            "get_memory" => await ExecuteGetMemoryAsync(arguments),
            "upload_file" => await ExecuteUploadFileAsync(arguments),
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
        };
    }

    private async Task<string> ExecuteSearchAsync(Dictionary<string, object> arguments)
    {
        var query = arguments["query"].ToString() ?? "";
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty");
        }
        if (query.Length > 10000)
        {
            throw new ArgumentException("Query exceeds maximum length of 10,000 characters");
        }

        var topK = arguments.ContainsKey("topK") ? Convert.ToInt32(arguments["topK"]) : 3;
        topK = Math.Min(topK, 10);

        var results = await _embeddings.SearchAsync(query, topK);
        return JsonSerializer.Serialize(new { results }, _jsonOptions);
    }

    private async Task<string> ExecuteAddMemoryAsync(Dictionary<string, object> arguments)
    {
        var text = arguments["text"].ToString() ?? "";
        var sourceDocument = arguments["sourceDocument"].ToString() ?? "";

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty");
        }
        if (text.Length > 1_000_000)
        {
            throw new ArgumentException("Text exceeds maximum length of 1,000,000 characters");
        }
        if (string.IsNullOrWhiteSpace(sourceDocument))
        {
            throw new ArgumentException("SourceDocument cannot be empty");
        }
        if (sourceDocument.Length > 500)
        {
            throw new ArgumentException("SourceDocument exceeds maximum length of 500 characters");
        }

        var id = await _embeddings.AddMemoryAsync(text, sourceDocument);
        return JsonSerializer.Serialize(new { id, message = "Memory stored successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteDeleteMemoryAsync(Dictionary<string, object> arguments)
    {
        var id = Convert.ToInt32(arguments["id"]);

        await _embeddings.DeleteMemoryAsync(id);
        return JsonSerializer.Serialize(new { message = $"Memory {id} deleted successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteGetMemoryAsync(Dictionary<string, object> arguments)
    {
        var id = Convert.ToInt32(arguments["id"]);

        var memory = await _embeddings.GetMemoryAsync(id);
        if (memory == null)
        {
            return JsonSerializer.Serialize(new { error = "Memory not found" }, _jsonOptions);
        }

        return JsonSerializer.Serialize(memory, _jsonOptions);
    }

    private async Task<string> ExecuteUploadFileAsync(Dictionary<string, object> arguments)
    {
        var text = arguments["text"].ToString() ?? "";
        var sourceDocument = arguments["sourceDocument"].ToString() ?? "";

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty");
        }
        if (text.Length > 5_000_000)
        {
            throw new ArgumentException("Text exceeds maximum length of 5,000,000 characters");
        }
        if (string.IsNullOrWhiteSpace(sourceDocument))
        {
            throw new ArgumentException("SourceDocument cannot be empty");
        }
        if (sourceDocument.Length > 500)
        {
            throw new ArgumentException("SourceDocument exceeds maximum length of 500 characters");
        }

        var result = await _embeddings.UploadFileAsync(text, sourceDocument);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }
}
