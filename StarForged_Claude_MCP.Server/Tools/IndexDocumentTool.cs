using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class IndexDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<IndexDocumentTool> _logger;

    public IndexDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<IndexDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "index_document",
        Description = "Chunks and embeds a document so that search_index can find it. Editing a document that is already indexed re-indexes it on its own, so this is only needed for one stored unindexed. Requires that request_write_permission has been called for the category.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to index" }
            },
            required = new[] { "category", "filename" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        _guards.RequireWriteEnabled(category);

        _logger.LogDebug("Executing index_document: category={Category}, filename={Filename}", category, filename);

        var indexed = await _documents.IndexDocumentAsync(category, filename);

        return ToolGuards.RequireDocumentWasFound(indexed, category, filename, message: "Document indexed successfully");
    }
}
