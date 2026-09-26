using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class DeindexDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<DeindexDocumentTool> _logger;

    public DeindexDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<DeindexDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "deindex_document",
        Description = "Removes a document's embeddings, so search_index stops returning chunks of it. The document and its content are left alone, and index_document puts it back. Requires that request_write_permission has been called for the category.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to remove from the index" }
            },
            required = new[] { "category", "filename" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        _guards.RequireWriteEnabled(category);

        _logger.LogInformation("Executing deindex_document: category={Category}, filename={Filename}", category, filename);

        var deindexed = await _documents.DeindexDocumentAsync(category, filename);

        return ToolGuards.RequireDocumentWasFound(deindexed, category, filename, message: "Document removed from the index successfully");
    }
}
