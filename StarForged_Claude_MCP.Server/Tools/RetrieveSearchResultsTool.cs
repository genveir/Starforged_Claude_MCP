using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class RetrieveSearchResultsTool : ITool
{
    private readonly IEmbeddingsFacade _embeddings;
    private readonly ILogger<RetrieveSearchResultsTool> _logger;

    public RetrieveSearchResultsTool(IEmbeddingsFacade embeddings, ILogger<RetrieveSearchResultsTool> logger)
    {
        _embeddings = embeddings;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
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
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var ids = arguments.RequireIntArray("ids");

        if (ids.Length == 0)
            throw new ArgumentException("Ids cannot be empty");
        if (ids.Length > 50)
            throw new ArgumentException("Cannot retrieve more than 50 ids at once");

        _logger.LogDebug("Executing retrieve_search_results: {IdCount} id(s)", ids.Length);
        var results = await _embeddings.RetrieveByIdsAsync(ids);
        _logger.LogDebug("retrieve_search_results returned {ResultCount} result(s)", results.Length);
        return McpJson.Serialize(new { results });
    }
}
