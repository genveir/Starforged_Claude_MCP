using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class DeleteDocumentSectionTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<DeleteDocumentSectionTool> _logger;

    public DeleteDocumentSectionTool(IDocumentsFacade documents, ToolGuards guards, ILogger<DeleteDocumentSectionTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "delete_document_section",
        Description = "Deletes one section of a document, leaving the rest of the file untouched. A section runs to the next header at the same or a higher level, so deleting a '#' section deletes every '##' and '###' section beneath it as well: name the exact section meant, and prefer the smallest one that covers what should go. An indexed document is re-indexed from what is left. Requires that request_write_permission has been called for the category. tag::section-editing",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to edit" },
                section = new { type = "string", description = ToolDescriptions.Section("delete") },
                summary = new { type = "string", description = ToolDescriptions.ReplacementSummary }
            },
            required = new[] { "category", "filename", "section" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var section = arguments.RequireString("section", maxLength: 1_000);
        var summary = arguments.OptionalStringAllowingEmpty("summary", maxLength: 512);
        _guards.RequireWriteEnabled(category);

        _logger.LogInformation("Executing delete_document_section: category={Category}, filename={Filename}, section={Section}",
            category, filename, section);

        var deleted = await _documents.DeleteSectionAsync(category, filename, section, summary);

        return ToolGuards.RequireDocumentWasFound(deleted, category, filename, message: "Section deleted successfully");
    }
}
