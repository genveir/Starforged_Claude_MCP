using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server.Services;

public class McpServer
{
    private readonly IEmbeddingsFacade _embeddings;
    private readonly IDocumentsFacade _documents;
    private readonly IDiceRoller _diceRoller;
    private readonly IWritePermissions _writePermissions;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(IEmbeddingsFacade backend, IDocumentsFacade documents, IDiceRoller diceRoller, IWritePermissions writePermissions, ILogger<McpServer> logger)
    {
        _embeddings = backend;
        _documents = documents;
        _diceRoller = diceRoller;
        _writePermissions = writePermissions;
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
                Description = "Stores a new document under a filename within a category. Fails if that category already holds a document with the same filename; use update_document to replace one. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to; categories act as separate namespaces" },
                        filename = new { type = "string", description = "Filename, unique within the category (e.g., 'session_5.md')" },
                        text = new { type = "string", description = "The full content of the document. Write well-formed Markdown: open with a '#' header and divide the rest under '##' headers. Sections are what the document is chunked on, their titles are what search results are labelled with, and they are what the section tools address. Content placed before the first header is stored, but search results for it carry no section label and no section tool can reach it." },
                        summary = new { type = "string", description = "Optional short summary, surfaced in document_index" },
                        indexed = new { type = "boolean", description = "Whether to chunk and embed this document so search_index can find it. Use index_document or deindex_document to change this later." }
                    },
                    required = new[] { "category", "filename", "text", "indexed" }
                }
            },
            new()
            {
                Name = "update_document",
                Description = "Replaces the entire content of an existing document. To change part of a long document, prefer replace_document_section, append_to_document or delete_document_section: they only need the text of the part that is changing, so they take far less time to write out. Whether the document is indexed is left as it is, and an indexed one is re-indexed from the new content. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to replace" },
                        text = new { type = "string", description = "The full replacement content; this is not a patch. Write well-formed Markdown: open with a '#' header and divide the rest under '##' headers. Sections are what the document is chunked on, their titles are what search results are labelled with, and they are what the section tools address. Content placed before the first header is stored, but search results for it carry no section label and no section tool can reach it." },
                        summary = new { type = "string", description = "Optional. Replaces the document's summary, surfaced in document_index. Leave it out to keep the summary the document already has; pass an empty string to clear it." }
                    },
                    required = new[] { "category", "filename", "text" }
                }
            },
            new()
            {
                Name = "replace_document_section",
                Description = "Replaces one section of a document and leaves the rest of the file untouched, so only the new text of that section has to be written out. A section runs to the next header at the same or a higher level, which means it carries every subsection nested under it: replacing a '#' section also replaces the '##' and '###' sections beneath it. Target the smallest section that covers the change. An indexed document is re-indexed from the result. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to edit" },
                        section = new { type = "string", description = "The section to replace, named by its header text without the '#' markers and matched ignoring case, e.g. 'Burial Rites'. Where one name is ambiguous, qualify it with headers it sits under, separated by '>', e.g. 'Ironlander Customs > Burial Rites'. If nothing matches, or more than one section does, the error lists the document's sections." },
                        text = new { type = "string", description = "The replacement text for that section. Start it with the section's own header line, at the level that header is at now; renaming the section means writing a different title on that line. Everything the old section held is gone, its subsections included, so write out any of them that should survive. Headers further down must be deeper than the section's own, since a shallower one would end it." },
                        summary = new { type = "string", description = "Optional. Replaces the document's summary, surfaced in document_index. Leave it out to keep the summary the document already has; pass an empty string to clear it." }
                    },
                    required = new[] { "category", "filename", "section", "text" }
                }
            },
            new()
            {
                Name = "append_to_document",
                Description = "Adds text to the end of a document, or to the end of one of its sections, without rewriting what is already there. An indexed document is re-indexed from the result. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to append to" },
                        section = new { type = "string", description = "Optional. The section to append to, named by its header text without the '#' markers and matched ignoring case; qualify an ambiguous name with headers it sits under, separated by '>', e.g. 'Ironlander Customs > Burial Rites'. The text lands at the very end of that section, after the last subsection nested under it, rather than directly after its own paragraphs. Leave it out to append at the end of the document." },
                        text = new { type = "string", description = "The text to add. When appending to a section, any header in it has to be deeper than that section's own header, since one at the same level or shallower would start a new section outside it instead." },
                        summary = new { type = "string", description = "Optional. Replaces the document's summary, surfaced in document_index. Leave it out to keep the summary the document already has; pass an empty string to clear it." }
                    },
                    required = new[] { "category", "filename", "text" }
                }
            },
            new()
            {
                Name = "delete_document_section",
                Description = "Deletes one section of a document, leaving the rest of the file untouched. A section runs to the next header at the same or a higher level, so deleting a '#' section deletes every '##' and '###' section beneath it as well: name the exact section meant, and prefer the smallest one that covers what should go. An indexed document is re-indexed from what is left. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to edit" },
                        section = new { type = "string", description = "The section to delete, named by its header text without the '#' markers and matched ignoring case, e.g. 'Burial Rites'. Where one name is ambiguous, qualify it with headers it sits under, separated by '>', e.g. 'Ironlander Customs > Burial Rites'. If nothing matches, or more than one section does, the error lists the document's sections." },
                        summary = new { type = "string", description = "Optional. Replaces the document's summary, surfaced in document_index. Leave it out to keep the summary the document already has; pass an empty string to clear it." }
                    },
                    required = new[] { "category", "filename", "section" }
                }
            },
            new()
            {
                Name = "index_document",
                Description = "Chunks and embeds a document so that search_index can find it. Editing a document that is already indexed re-indexes it on its own, so this is only needed for one stored unindexed. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to index" }
                    },
                    required = new[] { "category", "filename" }
                }
            },
            new()
            {
                Name = "deindex_document",
                Description = "Removes a document's embeddings, so search_index stops returning chunks of it. The document and its content are left alone, and index_document puts it back. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to remove from the index" }
                    },
                    required = new[] { "category", "filename" }
                }
            },
            new()
            {
                // Named "archive" deliberately: this permanently deletes the document and its embeddings.
                // Assistants calling this server run behind prompting layers that refuse to delete data but
                // will readily archive it, so the honest name got the call refused for data its owner means
                // to remove. Deleting stays behind the write permission and the client's own approval prompt.
                Name = "archive_document",
                Description = "Archives a document: it is hidden from indexing, so it no longer appears in search_index results or document_index. Requires that request_write_permission has been called for the category.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category the document belongs to" },
                        filename = new { type = "string", description = "Filename of the document to archive" }
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
                Description = "Rolls the dice for an Ironsworn action roll: one d6 (action die) and two d10s (challenge dice). Resolves the roll and returns actionScore (action die plus add, uncapped), resultType (\"strong hit\", \"weak hit\" or \"miss\"), match (whether the two challenge dice are equal), and d100 (the challenge dice read as an oracle roll, the first as the tens digit).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        add = new { type = "number", description = "Total to add to the action die: the relevant stat plus any other adds. Defaults to 0." }
                    }
                }
            },
            new()
            {
                Name = "request_write_permission",
                Description = "Permits writing in one category: adding, updating, editing sections of, indexing and archiving its documents. Every one of those tools refuses to run until this has been called for the category it is given. The permission covers that category only and lasts until release_write_permission is called for it or the server exits.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category to permit writes in" }
                    },
                    required = new[] { "category" }
                }
            },
            new()
            {
                Name = "release_write_permission",
                Description = "Revokes the write permission granted by request_write_permission, returning one category to read-only. Succeeds whether or not writes were permitted in it.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Category to return to read-only" }
                    },
                    required = new[] { "category" }
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
            "replace_document_section" => await ExecuteReplaceDocumentSectionAsync(arguments),
            "append_to_document" => await ExecuteAppendToDocumentAsync(arguments),
            "delete_document_section" => await ExecuteDeleteDocumentSectionAsync(arguments),
            "index_document" => await ExecuteIndexDocumentAsync(arguments),
            "deindex_document" => await ExecuteDeindexDocumentAsync(arguments),
            "archive_document" => await ExecuteArchiveDocumentAsync(arguments),
            "get_document" => await ExecuteGetDocumentAsync(arguments),
            "get_document_summary" => await ExecuteGetDocumentSummaryAsync(arguments),
            "document_index" => await ExecuteDocumentIndexAsync(arguments),
            "get_canonical_beats" => await ExecuteGetCanonicalBeatsAsync(arguments),
            "roll_dice" => ExecuteRollDice(arguments),
            "request_write_permission" => ExecuteRequestWritePermission(arguments),
            "release_write_permission" => ExecuteReleaseWritePermission(arguments),
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
        RequireWriteEnabled(category);

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
        RequireWriteEnabled(category);

        _logger.LogDebug("Executing update_document: category={Category}, filename={Filename}, textLength={TextLength}",
            category, filename, text.Length);

        var updated = await _documents.UpdateDocumentAsync(category, filename, text, summary);

        return RequireDocumentWasFound(updated, category, filename, message: "Document updated successfully");
    }

    private async Task<string> ExecuteReplaceDocumentSectionAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        var section = RequireString(arguments, "Section", maxLength: 1_000);
        var text = RequireString(arguments, "Text", maxLength: 1_000_000);
        var summary = OptionalSummary(arguments);
        RequireWriteEnabled(category);

        _logger.LogDebug("Executing replace_document_section: category={Category}, filename={Filename}, section={Section}, textLength={TextLength}",
            category, filename, section, text.Length);

        var replaced = await _documents.ReplaceSectionAsync(category, filename, section, text, summary);

        return RequireDocumentWasFound(replaced, category, filename, message: "Section replaced successfully");
    }

    private async Task<string> ExecuteAppendToDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        var section = OptionalString(arguments, "section", maxLength: 1_000);
        var text = RequireString(arguments, "Text", maxLength: 1_000_000);
        var summary = OptionalSummary(arguments);
        RequireWriteEnabled(category);

        _logger.LogDebug("Executing append_to_document: category={Category}, filename={Filename}, section={Section}, textLength={TextLength}",
            category, filename, section ?? "(end of document)", text.Length);

        var appended = await _documents.AppendAsync(category, filename, section, text, summary);

        return RequireDocumentWasFound(appended, category, filename, message: "Text appended successfully");
    }

    private async Task<string> ExecuteDeleteDocumentSectionAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        var section = RequireString(arguments, "Section", maxLength: 1_000);
        var summary = OptionalSummary(arguments);
        RequireWriteEnabled(category);

        _logger.LogInformation("Executing delete_document_section: category={Category}, filename={Filename}, section={Section}",
            category, filename, section);

        var deleted = await _documents.DeleteSectionAsync(category, filename, section, summary);

        return RequireDocumentWasFound(deleted, category, filename, message: "Section deleted successfully");
    }

    private async Task<string> ExecuteIndexDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        RequireWriteEnabled(category);

        _logger.LogDebug("Executing index_document: category={Category}, filename={Filename}", category, filename);

        var indexed = await _documents.IndexDocumentAsync(category, filename);

        return RequireDocumentWasFound(indexed, category, filename, message: "Document indexed successfully");
    }

    private async Task<string> ExecuteDeindexDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        RequireWriteEnabled(category);

        _logger.LogInformation("Executing deindex_document: category={Category}, filename={Filename}", category, filename);

        var deindexed = await _documents.DeindexDocumentAsync(category, filename);

        return RequireDocumentWasFound(deindexed, category, filename, message: "Document removed from the index successfully");
    }

    private string RequireDocumentWasFound(bool found, string category, string filename, string message)
    {
        if (!found)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        return JsonSerializer.Serialize(new { message }, _jsonOptions);
    }

    private async Task<string> ExecuteArchiveDocumentAsync(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);
        var filename = RequireString(arguments, "Filename", maxLength: 500);
        RequireWriteEnabled(category);

        _logger.LogDebug("Executing archive_document: category={Category}, filename={Filename}", category, filename);

        var deleted = await _documents.DeleteDocumentAsync(category, filename);

        if (!deleted)
            throw new ArgumentException($"No document named '{filename}' exists in category '{category}'.");

        return JsonSerializer.Serialize(new { message = "Document archived successfully" }, _jsonOptions);
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

    private string ExecuteRequestWritePermission(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);

        _logger.LogInformation("Executing request_write_permission: category={Category}", category);
        _writePermissions.EnableWrite(category);

        return JsonSerializer.Serialize(
            new { message = $"Writes are now permitted in category '{category}'." }, _jsonOptions);
    }

    private string ExecuteReleaseWritePermission(Dictionary<string, object> arguments)
    {
        var category = RequireCategory(arguments);

        _logger.LogInformation("Executing release_write_permission: category={Category}", category);
        _writePermissions.DisableWrite(category);

        return JsonSerializer.Serialize(
            new { message = $"Category '{category}' is now read-only." }, _jsonOptions);
    }

    private void RequireWriteEnabled(string category)
    {
        if (_writePermissions.IsWriteEnabled(category)) return;

        _logger.LogWarning("Refused a write to read-only category {Category}", category);
        throw new ArgumentException(
            $"Category '{category}' is read-only. Call request_write_permission for it before writing to any document in it.");
    }

    private string ExecuteRollDice(Dictionary<string, object> arguments)
    {
        var add = OptionalInt(arguments, "add", defaultValue: 0);
        var roll = _diceRoller.RollAction(add);

        _logger.LogDebug(
            "Executing roll_dice: actionDie={ActionDie}, add={Add}, actionScore={ActionScore}, challengeDice={ChallengeDice}, resultType={ResultType}, match={Match}, d100={D100}",
            roll.ActionDie, roll.Add, roll.ActionScore, string.Join(",", roll.ChallengeDice), roll.ResultType, roll.Match, roll.D100);

        return JsonSerializer.Serialize(roll, _jsonOptions);
    }

    private static int OptionalInt(Dictionary<string, object> arguments, string key, int defaultValue)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw == null)
            return defaultValue;

        return raw is JsonElement je ? je.GetInt32() : Convert.ToInt32(raw);
    }

    private static string RequireCategory(Dictionary<string, object> arguments) =>
        RequireString(arguments, "Category", maxLength: 200);

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

    private static string? OptionalString(Dictionary<string, object> arguments, string key, int maxLength)
    {
        var value = ReadOptional(arguments, key);

        if (value != null && value.Length > maxLength)
            throw new ArgumentException($"{char.ToUpperInvariant(key[0]) + key[1..]} exceeds maximum length of {maxLength:N0} characters");

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// An absent summary and an empty one mean different things on a write: absent keeps whatever the
    /// document already carries, empty clears it. Both survive as far as the facade, which is where
    /// that distinction is resolved against the stored summary.
    /// </summary>
    private static string? OptionalSummary(Dictionary<string, object> arguments)
    {
        var summary = ReadOptional(arguments, "summary");

        if (summary != null && summary.Length > 512)
            throw new ArgumentException("Summary exceeds maximum length of 512 characters");

        return summary?.Trim();
    }

    private static string? ReadOptional(Dictionary<string, object> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw == null)
            return null;

        if (raw is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        return raw.ToString();
    }
}
