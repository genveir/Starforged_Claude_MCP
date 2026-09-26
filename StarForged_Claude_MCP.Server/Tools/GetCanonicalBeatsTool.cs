using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class GetCanonicalBeatsTool : ITool
{
    private readonly IDocumentsFacade _documents;
    private readonly ToolGuards _guards;
    private readonly ILogger<GetCanonicalBeatsTool> _logger;

    public GetCanonicalBeatsTool(IDocumentsFacade documents, ToolGuards guards, ILogger<GetCanonicalBeatsTool> logger)
    {
        _documents = documents;
        _guards = guards;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_canonical_beats",
        Description = "Retrieves the canonical beats of a session in narrative order. A beat that was later rewritten is returned in its original position with its newest content; superseded versions are not returned. Beats with no number of their own, such as vignettes and interludes, are returned in the order they were written.",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "Leaf category the session belongs to: " + ToolDescriptions.LeafCategoryRule },
                sessionNumber = new { type = "number", description = "The session number to retrieve beats for" }
            },
            required = new[] { "category", "sessionNumber" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var category = arguments.RequireCategory();
        var sessionNumber = arguments.RequireInt("sessionNumber");
        await _guards.RequireLeafCategoryAsync(category);

        _logger.LogDebug("Executing get_canonical_beats: category={Category}, sessionNumber={SessionNumber}", category, sessionNumber);
        var beats = await _documents.GetCanonicalBeatsAsync(category, sessionNumber);
        _logger.LogDebug("get_canonical_beats returned {BeatCount} beat(s)", beats.Count);
        return McpJson.Serialize(new { beats });
    }
}
