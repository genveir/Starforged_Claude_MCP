using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Impacts;

public class SetImpactTool : ITool
{
    private readonly IImpactService _impacts;
    private readonly ILogger<SetImpactTool> _logger;

    public SetImpactTool(IImpactService impacts, ILogger<SetImpactTool> logger)
    {
        _impacts = impacts;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "set_impact",
        Description = "Marks or clears an impact on the character, a vehicle or a module. Marking an impact that is already marked, or clearing one that is not, changes nothing. Returns all of the campaign's impacts as {\"impacts\": [{\"entity\", \"name\"}, ...]}, ordered by entity and then name. tag::ironsworn::impacts",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                entity = new { type = "string", description = ToolDescriptions.ImpactEntity },
                name = new { type = "string", description = ToolDescriptions.ImpactName },
                marked = new { type = "boolean", description = "true to mark the impact, false to clear it." }
            },
            required = new[] { "campaign", "entity", "name", "marked" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var entity = arguments.RequireStateId("entity");
        var name = arguments.RequireString("name", maxLength: 100).Trim();
        var marked = arguments.RequireBool("marked");

        _logger.LogDebug(
            "Executing set_impact: campaign={Campaign}, entity={Entity}, name={Name}, marked={Marked}",
            campaign.Value, entity.Value, name, marked);

        var impacts = marked
            ? await _impacts.MarkImpact(campaign, entity, name)
            : await _impacts.ClearImpact(campaign, entity, name);

        return impacts.Map(
            onSuccess: all => McpJson.Serialize(new { impacts = all.Select(ImpactRecord.From) }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
