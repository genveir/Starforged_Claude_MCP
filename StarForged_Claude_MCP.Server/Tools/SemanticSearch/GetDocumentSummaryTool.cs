using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.SemanticSearch;

public class GetDocumentSummaryTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<GetDocumentSummaryTool> _logger;

    public GetDocumentSummaryTool(IDocumentsFacade documents, ToolGuards guards, ILogger<GetDocumentSummaryTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_document_summary",
        Description = "Retrieves one document's summary without its content. Useful after search_index, where several chunks of one file can be returned at once: fetch the file's summary once to see what it is, rather than judging it from each chunk. The summary is null for documents stored without one. tag::semantic-search",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document, as search results and document listings give it" }
            },
            required = new[] { "category", "filename" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);

        await _guards.RequireLeafCategoryAsync(category);

        _logger.LogDebug("Executing get_document_summary: category={Category}, filename={Filename}", category, filename);

        var entry = await _documents.GetDocumentSummaryAsync(category, filename);

        if (entry == null)
            throw ToolGuards.NoSuchDocument(category, filename);

        // Written out by hand so that a missing summary comes back as an explicit null rather
        // than an absent field, which would read as though the document had no summary field at all.
        return McpJson.Serialize(new
        {
            filename = entry.Filename,
            summary = entry.Summary,
            indexed = entry.Indexed
        });
    }
}
