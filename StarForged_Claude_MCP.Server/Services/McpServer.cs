using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server.Services;

public class McpServer
{
    private readonly EmbeddingsFacade _embeddings;
    private readonly DocumentsFacade _documents;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(EmbeddingsFacade backend, DocumentsFacade documents, ILogger<McpServer> logger)
    {
        _embeddings = backend;
        _documents = documents;
        _logger = logger;
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

                // Notifications have no id and must not receive a response
                if (request.Id == null) continue;

                _logger.LogDebug("Received request: {Method} (id={Id})", request.Method, request.Id);
                var response = await HandleRequestAsync(request);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
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
                Name = "add_searchable",
                Description = "Chunks and stores text and makes it searchable",
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
                Name = "add_document",
                Description = "Stores a document verbatim, other documents with the same sourceDocument will be stored and retrievable in sequence.",
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
                Name = "get_documents",
                Description = "Retrieves all documents stored with the given sourceDocument in the order they were added.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceDocument = new { type = "string", description = "Category or identifier (e.g., 'campaign_session_5')" }
                    },
                    required = new[] { "sourceDocument" }
                }
            },
            new()
            {
                Name = "get_canonical_beats",
                Description = "Retrieves the canonical beats for a given session in the order they were added. Stored documents are full GM responses; beats are embedded within them alongside mechanical confirmations and conversational content.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionNumber = new { type = "number", description = "The session number to retrieve beats for" }
                    },
                    required = new[] { "sessionNumber" }
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
            _logger.LogWarning("Tool call argument error: {Message}", ex.Message);
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
        return toolName switch
        {
            "search" => await ExecuteSearchAsync(arguments),
            "add_memory" => await ExecuteAddMemoryAsync(arguments),
            "add_document" => await ExecuteAddDocumentAsync(arguments),
            "get_documents" => await ExecuteGetDocumentsAsync(arguments),
            "get_canonical_beats" => await ExecuteGetCanonicalBeatsAsync(arguments),
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

        var topK = arguments.ContainsKey("topK")
            ? (arguments["topK"] is JsonElement je ? je.GetInt32() : Convert.ToInt32(arguments["topK"]))
            : 3;
        topK = Math.Min(topK, 10);

        _logger.LogDebug("Executing search: query length={QueryLength}, topK={TopK}", query.Length, topK);
        var results = await _embeddings.SearchAsync(query, topK);
        _logger.LogDebug("Search returned {ResultCount} result(s)", results.Length);
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

        _logger.LogDebug("Executing add_searchable: sourceDocument={SourceDocument}, textLength={TextLength}", sourceDocument, text.Length);
        var id = await _embeddings.AddMemoryAsync(text, sourceDocument);
        _logger.LogDebug("add_searchable stored {ChunkCount} chunk(s) for sourceDocument={SourceDocument}", id.Length, sourceDocument);
        return JsonSerializer.Serialize(new { message = "Memory stored successfully", Id = id }, _jsonOptions);
    }

    private async Task<string> ExecuteAddDocumentAsync(Dictionary<string, object> arguments)
    {
        var text = arguments["text"].ToString() ?? "";
        var sourceDocument = arguments["sourceDocument"].ToString() ?? "";

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty");
        if (text.Length > 1_000_000)
            throw new ArgumentException("Text exceeds maximum length of 1,000,000 characters");
        if (string.IsNullOrWhiteSpace(sourceDocument))
            throw new ArgumentException("SourceDocument cannot be empty");
        if (sourceDocument.Length > 500)
            throw new ArgumentException("SourceDocument exceeds maximum length of 500 characters");

        _logger.LogDebug("Executing add_document: sourceDocument={SourceDocument}, textLength={TextLength}", sourceDocument, text.Length);
        await _documents.StoreDocumentAsync(text, sourceDocument);
        _logger.LogDebug("add_document stored document for sourceDocument={SourceDocument}", sourceDocument);
        return JsonSerializer.Serialize(new { message = "Document stored successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteGetDocumentsAsync(Dictionary<string, object> arguments)
    {
        var sourceDocument = arguments["sourceDocument"].ToString() ?? "";

        if (string.IsNullOrWhiteSpace(sourceDocument))
            throw new ArgumentException("SourceDocument cannot be empty");
        if (sourceDocument.Length > 500)
            throw new ArgumentException("SourceDocument exceeds maximum length of 500 characters");

        _logger.LogDebug("Executing get_documents: sourceDocument={SourceDocument}", sourceDocument);
        var documents = await _documents.GetDocumentsAsync(sourceDocument);
        _logger.LogDebug("get_documents returned {DocumentCount} document(s) for sourceDocument={SourceDocument}", documents.Count, sourceDocument);
        return JsonSerializer.Serialize(new { documents }, _jsonOptions);
    }

    private async Task<string> ExecuteGetCanonicalBeatsAsync(Dictionary<string, object> arguments)
    {
        var sessionNumber = arguments["sessionNumber"] is JsonElement je
            ? je.GetInt32()
            : Convert.ToInt32(arguments["sessionNumber"]);

        var sourceDocument = $"SessionBeats_{sessionNumber}";

        _logger.LogDebug("Executing get_canonical_beats: sessionNumber={SessionNumber}", sessionNumber);
        var allDocuments = await _documents.GetDocumentsAsync(sourceDocument);
        var documents = FilterCanonicalBeats(allDocuments);
        _logger.LogDebug("get_canonical_beats returned {DocumentCount} document(s) for sessionNumber={SessionNumber}", documents.Count, sessionNumber);
        return JsonSerializer.Serialize(new { documents }, _jsonOptions);
    }

    private static List<DocumentResult> FilterCanonicalBeats(List<DocumentResult> documents)
    {
        var maxVersionByN = documents
            .Where(d => d.BeatNumber != null)
            .GroupBy(d => int.Parse(d.BeatNumber!.Split('.')[0]))
            .ToDictionary(g => g.Key, g => g.Max(d => int.Parse(d.BeatNumber!.Split('.')[1])));

        var filtered = documents
            .Where(d =>
            {
                if (d.BeatNumber == null) return true;
                var parts = d.BeatNumber.Split('.');
                var n = int.Parse(parts[0]);
                var v = int.Parse(parts[1]);
                return v == maxVersionByN[n];
            })
            .ToList();

        int sequence = 1;
        foreach (var doc in filtered)
            doc.Sequence = sequence++;

        return filtered;
    }
}
