using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class UpdateDocumentTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<UpdateDocumentTool> _logger;

    public UpdateDocumentTool(IDocumentsFacade documents, ToolGuards guards, ILogger<UpdateDocumentTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "update_document",
        Description = "Replaces the entire content of an existing document. To change part of a long document, prefer replace_document_section, append_to_document or delete_document_section: they only need the text of the part that is changing, so they take far less time to write out. Whether the document is indexed is left as it is, and an indexed one is re-indexed from the new content. Requires that request_write_permission has been called for the category.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to replace" },
                text = new { type = "string", description = "The full replacement content; this is not a patch. " + ToolDescriptions.MarkdownStructure },
                summary = new { type = "string", description = ToolDescriptions.ReplacementSummary }
            },
            required = new[] { "category", "filename", "text" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var text = arguments.RequireString("text", maxLength: 1_000_000);
        var summary = arguments.OptionalSummary();
        _guards.RequireWriteEnabled(category);

        _logger.LogDebug("Executing update_document: category={Category}, filename={Filename}, textLength={TextLength}",
            category, filename, text.Length);

        var updated = await _documents.UpdateDocumentAsync(category, filename, text, summary);

        return ToolGuards.RequireDocumentWasFound(updated, category, filename, message: "Document updated successfully");
    }
}
