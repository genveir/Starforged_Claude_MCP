using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class ReplaceSectionTextTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<ReplaceSectionTextTool> _logger;

    public ReplaceSectionTextTool(IDocumentsFacade documents, ToolGuards guards, ILogger<ReplaceSectionTextTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "replace_section_text",
        Description = "Replaces every occurrence of a literal snippet of text within one section, leaving the rest of the section and the rest of the document untouched. Prefer this over replace_document_section for a small change, since only the changed snippet has to be written out rather than the whole section. Reports how many occurrences were replaced. An indexed document is re-indexed from the result. Requires that request_write_permission has been called for the category. tag::section-editing",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = ToolDescriptions.LeafCategory },
                filename = new { type = "string", description = "Filename of the document to edit" },
                section = new { type = "string", description = ToolDescriptions.Section("edit") },
                oldText = new { type = "string", description = "The exact text to find within that section, matched literally and case-sensitively. Every occurrence in the section is replaced. Refused if it does not appear in the section at all." },
                newText = new { type = "string", description = "The text to put in place of every occurrence of oldText. An empty string deletes oldText outright." },
                summary = new { type = "string", description = ToolDescriptions.ReplacementSummary }
            },
            required = new[] { "category", "filename", "section", "oldText", "newText" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var filename = arguments.RequireString("filename", maxLength: 500);
        var section = arguments.RequireString("section", maxLength: 1_000);
        var oldText = arguments.RequireString("oldText", maxLength: 1_000_000);
        var newText = arguments.RequireStringAllowingEmpty("newText", maxLength: 1_000_000);
        var summary = arguments.OptionalStringAllowingEmpty("summary", maxLength: 512);
        _guards.RequireWriteEnabled(category);

        _logger.LogDebug("Executing replace_section_text: category={Category}, filename={Filename}, section={Section}, oldTextLength={OldTextLength}, newTextLength={NewTextLength}",
            category, filename, section, oldText.Length, newText.Length);

        var replacements = await _documents.ReplaceSectionTextAsync(category, filename, section, oldText, newText, summary);

        if (replacements == null)
            throw ToolGuards.NoSuchDocument(category, filename);

        return McpJson.Serialize(new { message = "Section text replaced successfully", replacements });
    }
}
