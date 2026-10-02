using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class SearchIndexTool : ITool
{
    private readonly IEmbeddingsFacade _embeddings;
    private readonly ILogger<SearchIndexTool> _logger;

    public SearchIndexTool(IEmbeddingsFacade embeddings, ILogger<SearchIndexTool> logger)
    {
        _embeddings = embeddings;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "search_index",
        Description = "Search for relevant chunks by semantic similarity within a category and every category under it. Only documents stored with indexed=true are searchable. Returns IDs, scores, the leaf category and filename each chunk came from, and brief summaries only — not full content. Use retrieve_search_results to fetch full text for relevant IDs, or get_document to fetch the whole file a chunk came from. To find where an exact name or term appears, use find_text instead. tag::semantic-search",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Natural language search query" },
                category = new { type = "string", description = ToolDescriptions.ScopeCategory },
                topK = new { type = "number", description = "Number of results to return (default: 3, max: 10)" }
            },
            required = new[] { "query", "category" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var query = arguments.RequireString("query", maxLength: 10_000);
        var category = arguments.RequireCategory();

        var topK = Math.Min(arguments.OptionalInt("topK", defaultValue: 3), 10);

        _logger.LogDebug("Executing search: category={Category}, query length={QueryLength}, topK={TopK}", category, query.Length, topK);
        var results = await _embeddings.SearchAsync(query, category, topK);
        _logger.LogDebug("Search returned {ResultCount} result(s)", results.Length);

        var briefResults = results.Select(r => new
        {
            id = r.Id,
            score = r.SimilarityScore,
            category = r.Category,
            filename = r.Filename,
            summary = r.BriefSummary
        }).ToArray();
        return McpJson.Serialize(new { results = briefResults });
    }
}
