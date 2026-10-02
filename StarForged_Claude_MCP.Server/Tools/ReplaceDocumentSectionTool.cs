using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class ReplaceDocumentSectionTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<ReplaceDocumentSectionTool> _logger;

    public ReplaceDocumentSectionTool(IDocumentsFacade documents, ToolGuards guards, ILogger<ReplaceDocumentSectionTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "replace_document_section",
        Description = "Replaces one section of a document and leaves the rest of the file untouched, so only the new text of that section has to be written out. A section runs to the next header at the same or a higher level, which means it carries every subsection nested under it: replacing a '#' section also replaces the '##' and '###' sections beneath it. Target the smallest section that covers the change. An indexed document is re-indexed from the result. Requires that request_write_permission has been called for the category. tag::section-editing",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to edit" },
                section = new { type = "string", description = ToolDescriptions.Section("replace") },
                text = new { type = "string", description = "The replacement text for that section. Start it with the section's own header line, at the level that header is at now; renaming the section means writing a different title on that line. Everything the old section held is gone, its subsections included, so write out any of them that should survive. Headers further down must be deeper than the section's own, since a shallower one would end it." },
                summary = new { type = "string", description = ToolDescriptions.ReplacementSummary }
            },
            required = new[] { "category", "filename", "section", "text" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var section = arguments.RequireString("section", maxLength: 1_000);
        var text = arguments.RequireString("text", maxLength: 1_000_000);
        var summary = arguments.OptionalSummary();
        _guards.RequireWriteEnabled(category);

        _logger.LogDebug("Executing replace_document_section: category={Category}, filename={Filename}, section={Section}, textLength={TextLength}",
            category, filename, section, text.Length);

        var replaced = await _documents.ReplaceSectionAsync(category, filename, section, text, summary);

        return ToolGuards.RequireDocumentWasFound(replaced, category, filename, message: "Section replaced successfully");
    }
}
