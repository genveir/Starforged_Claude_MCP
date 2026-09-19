using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server.Services;

public class McpServer
{
    private readonly IEmbeddingsFacade _embeddings;
    private readonly IDocumentsFacade _documents;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(IEmbeddingsFacade backend, IDocumentsFacade documents, ILogger<McpServer> logger)
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
                Name = "search_index",
                Description = "Search for relevant chunks by semantic similarity within a single category. Only documents stored with indexed=true are searchable. Returns IDs, scores, filenames and brief summaries only — not full content. Use retrieve_search_results to fetch full text for relevant IDs, or get_document to fetch the whole file a chunk came from",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language search query" },
                        category = new { type = "string", description = "Category to search; only chunks from documents in this category are considered" },
                        topK = new { type = "number", description = "Number of results to return (default: 3, max: 10)" }
                    },
                    required = new[] { "query", "category" }
                }
            },
            new()
            {
                Name = "retrieve_search_results",
                Description = "Retrieves full chunk text by ID. Results are returned in the exact same order as the provided IDs. IDs not found in the database are omitted. Use this after search_index to fetch full content for relevant IDs.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        ids = new { type = "array", items = new { type = "number" }, description = "Array of chunk IDs to retrieve, as returned by search_index. Results will be returned in this exact order." }
                    },
                    required = new[] { "ids" }
                }
            },
            new()
            {
                Name = "add_document",
                Description = "Stores a new document under a filename within a category. Fails if that category already holds a document with the same filename; use update_document to replace one.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to; categories act as separate namespaces" },
                        filename = new { type = "string", description = "Filename, unique within the category (e.g., 'session_5.md')" },
                        text = new { type = "string", description = "The full content of the document. Write well-formed Markdown with '#' and '##' headers: sections are what the document is chunked on, and their titles are what search results are labelled with. Content placed before the first header is stored, but search results for it carry no section label." },
                        summary = new { type = "string", description = "Optional short summary, surfaced in document_index" },
                        indexed = new { type = "boolean", description = "Whether to chunk and embed this document so search_index can find it" }
                    },
                    required = new[] { "category", "filename", "text", "indexed" }
                }
            },
            new()
            {
                Name = "update_document",
                Description = "Replaces the entire content of an existing document. Anything indexed for it is rebuilt from the new content, or removed when indexed is false.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to replace" },
                        text = new { type = "string", description = "The full replacement content; this is not a patch. Write well-formed Markdown with '#' and '##' headers: sections are what the document is chunked on, and their titles are what search results are labelled with." },
                        summary = new { type = "string", description = "Optional short summary, surfaced in document_index" },
                        indexed = new { type = "boolean", description = "Whether to chunk and embed this document so search_index can find it" }
                    },
                    required = new[] { "category", "filename", "text", "indexed" }
                }
            },
            new()
            {
                Name = "delete_document",
                Description = "Permanently deletes a document and anything indexed for it.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to delete" }
                    },
                    required = new[] { "category", "filename" }
                }
            },
            new()
            {
                Name = "get_document",
                Description = "Retrieves one document in full by category and filename, as listed by document_index.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to retrieve" }
                    },
                    required = new[] { "category", "filename" }
                }
            },
            new()
            {
                Name = "get_document_summary",
                Description = "Retrieves one document's summary without its content. Useful after search_index, where several chunks of one file can be returned at once: fetch the file's summary once to see what it is, rather than judging it from each chunk. The summary is null for documents stored without one.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document, as returned by search_index or document_index" }
                    },
                    required = new[] { "category", "filename" }
                }
            },
            new()
            {
                Name = "document_index",
                Description = "Lists the documents in a category with their summaries, without their content. Use get_document to fetch one in full.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category to list" }
                    },
                    required = new[] { "category" }
                }
            },
            new()
            {
                Name = "get_canonical_beats",
                Description = "Retrieves the canonical beats of a session in narrative order. A beat that was later rewritten is returned in its original position with its newest content; superseded versions are not returned. Beats with no number of their own, such as vignettes and interludes, are returned in the order they were written.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the session belongs to" },
                        sessionNumber = new { type = "number", description = "The session number to retrieve beats for" }
                    },
                    required = new[] { "category", "sessionNumber" }
                }
            },
            new()
            {
                Name = "roll_dice",
                Description = "Rolls the dice for an Ironsworn action roll: one d6 (action die) and two d10s (challenge dice). Returns the results as integers.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
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
            "search_index" => await ExecuteSearchAsync(arguments),
            "retrieve_search_results" => await ExecuteRetrieveSearchResultsAsync(arguments),
            "add_document" => await ExecuteAddDocumentAsync(arguments),
            "update_document" => await ExecuteUpdateDocumentAsync(arguments),
            "delete_document" => await ExecuteDeleteDocumentAsync(arguments),
            "get_document" => await ExecuteGetDocumentAsync(arguments),
            "get_document_summary" => await ExecuteGetDocumentSummaryAsync(arguments),
            "document_index" => await ExecuteDocumentIndexAsync(arguments),
            "get_canonical_beats" => await ExecuteGetCanonicalBeatsAsync(arguments),
            "roll_dice" => ExecuteRollDice(),
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
        };
    }

    private async Task<string> ExecuteSearchAsync(Dictionary<string, object> arguments)
    {
        var query = RequireString(arguments, "Query", maxLength: 10_000);
        var category = RequireCategory(arguments);

        var topK = arguments.ContainsKey("topK")
            ? (arguments["topK"] is JsonElement je ? je.GetInt32() : Convert.ToInt32(arguments["topK"]))
            : 3;
        topK = Math.Min(topK, 10);

        _logger.LogDebug("Executing search: category={Category}, query length={QueryLength}, topK={TopK}", category, query.Length, topK);
        var results = await _embeddings.SearchAsync(query, category, topK);
        _logger.LogDebug("Search returned {ResultCount} result(s)", results.Length);

        var briefResults = results.Select(r => new
        {
            id = r.Id,
            score = r.SimilarityScore,
            filename = r.Filename,
            summary = r.BriefSummary
        }).ToArray();
        return JsonSerializer.Serialize(new { results = briefResults }, _jsonOptions);
    }

    private async Task<string> ExecuteRetrieveSearchResultsAsync(Dictionary<string, object> arguments)
    {
        int[] ids;
        if (arguments["ids"] is JsonElement je)
        {
            ids = je.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        }
        else
        {
            ids = ((IEnumerable<object>)arguments["ids"]).Select(e => Convert.ToInt32(e)).ToArray();
        }

        if (ids.Length == 0)
            throw new ArgumentException("Ids cannot be empty");
        if (ids.Length > 50)
            throw new ArgumentException("Cannot retrieve more than 50 ids at once");

        _logger.LogDebug("Executing retrieve_search_results: {IdCount} id(s)", ids.Length);
        var results = await _embeddings.RetrieveByIdsAsync(ids);
        _logger.LogDebug("retrieve_search_results returned {ResultCount} result(s)", results.Length);
        return JsonSerializer.Serialize(new { results }, _jsonOptions);
    }

    private async Task<string> ExecuteAddDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        var text = RequireString(arguments, "Text", maxLength: 1_000_000);
        var summary = OptionalSummary(arguments);
        var indexed = RequireBool(arguments, "indexed");

        _logger.LogDebug("Executing add_document: category={Category}, filename={Filename}, indexed={Indexed}, textLength={TextLength}",
            category, filename, indexed, text.Length);

        var stored = await _documents.AddDocumentAsync(category, filename, text, summary, indexed);

        if (!stored)
            throw new ArgumentException($"A document named '{filename}' already exists in category '{category}'. Use update_document to replace it.");

        return JsonSerializer.Serialize(new { message = "Document stored successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteUpdateDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        var text = RequireString(arguments, "Text", maxLength: 1_000_000);
        var summary = OptionalSummary(arguments);
        var indexed = RequireBool(arguments, "indexed");

        _logger.LogDebug("Executing update_document: category={Category}, filename={Filename}, indexed={Indexed}, textLength={TextLength}",
            category, filename, indexed, text.Length);

        var updated = await _documents.UpdateDocumentAsync(category, filename, text, summary, indexed);

        if (!updated)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        return JsonSerializer.Serialize(new { message = "Document updated successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteDeleteDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);

        _logger.LogDebug("Executing delete_document: category={Category}, filename={Filename}", category, filename);

        var deleted = await _documents.DeleteDocumentAsync(category, filename);

        if (!deleted)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        return JsonSerializer.Serialize(new { message = "Document deleted successfully" }, _jsonOptions);
    }

    private async Task<string> ExecuteGetDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);

        _logger.LogDebug("Executing get_document: category={Category}, filename={Filename}", category, filename);

        var document = await _documents.GetDocumentAsync(category, filename);

        if (document == null)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        return JsonSerializer.Serialize(new { document }, _jsonOptions);
    }

    private async Task<string> ExecuteGetDocumentSummaryAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);

        _logger.LogDebug("Executing get_document_summary: category={Category}, filename={Filename}", category, filename);

        var entry = await _documents.GetDocumentSummaryAsync(category, filename);

        if (entry == null)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        // Written out by hand so that a missing summary comes back as an explicit null rather
        // than an absent field, which would read as though the document had no summary field at all.
        return JsonSerializer.Serialize(new
        {
            filename = entry.Filename,
            summary = entry.Summary,
            indexed = entry.Indexed
        }, _jsonOptions);
    }

    private async Task<string> ExecuteDocumentIndexAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);

        _logger.LogDebug("Executing document_index: category={Category}", category);
        var documents = await _documents.GetDocumentIndexAsync(category);
        _logger.LogDebug("document_index returned {Count} document(s)", documents.Count);
        return JsonSerializer.Serialize(new { documents }, _jsonOptions);
    }

    private async Task<string> ExecuteGetCanonicalBeatsAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var sessionNumber = arguments["sessionNumber"] is JsonElement je
            ? je.GetInt32()
            : Convert.ToInt32(arguments["sessionNumber"]);

        _logger.LogDebug("Executing get_canonical_beats: category={Category}, sessionNumber={SessionNumber}", category, sessionNumber);
        var beats = await _documents.GetCanonicalBeatsAsync(category, sessionNumber);
        _logger.LogDebug("get_canonical_beats returned {BeatCount} beat(s)", beats.Count);
        return JsonSerializer.Serialize(new { beats }, _jsonOptions);
    }

    private string ExecuteRollDice()
    {
        var actionDie = Random.Shared.Next(1, 7);
        var challengeDice = new[] { Random.Shared.Next(1, 11), Random.Shared.Next(1, 11) };

        _logger.LogDebug("Executing roll_dice: actionDie={ActionDie}, challengeDice={ChallengeDice}", actionDie, string.Join(",", challengeDice));
        return JsonSerializer.Serialize(new { actionDie, challengeDice }, _jsonOptions);
    }

    private static string RequireCategory(Dictionary<string, object> arguments) =>
        RequireString(arguments, "Category", maxLength: 200);

    /// <param name="name">The argument's name, capitalised for the error message; looked up camel-cased.</param>
    private static string RequireString(Dictionary<string, object> arguments, string name, int maxLength)
    {
        var key = char.ToLowerInvariant(name[0]) + name[1..];
        var value = arguments.TryGetValue(key, out var raw) ? raw?.ToString() : null;

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} cannot be empty");
        if (value.Length > maxLength)
            throw new ArgumentException($"{name} exceeds maximum length of {maxLength:N0} characters");

        return value;
    }

    private static bool RequireBool(Dictionary<string, object> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw == null)
            throw new ArgumentException($"{char.ToUpperInvariant(key[0]) + key[1..]} is required");

        return raw is JsonElement je ? je.GetBoolean() : Convert.ToBoolean(raw);
    }

    private static string? OptionalSummary(Dictionary<string, object> arguments)
    {
        var summary = arguments.TryGetValue("summary", out var raw) ? raw?.ToString() : null;

        if (summary != null && summary.Length > 512)
            throw new ArgumentException("Summary exceeds maximum length of 512 characters");

        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }
}
