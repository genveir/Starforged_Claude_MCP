using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Impacts;

public class GetImpactsTool : ITool
{
    private readonly IImpactService _impacts;
    private readonly ILogger<GetImpactsTool> _logger;

    public GetImpactsTool(IImpactService impacts, ILogger<GetImpactsTool> logger)
    {
        _impacts = impacts;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_impacts",
        Description = "Reads the impacts marked in the campaign: those on one entity, ordered by name, or all of them when entity is left out, ordered by entity and then name. An impact is returned as {\"entity\", \"name\"}; the impacts come back as {\"impacts\": [...]}. tag::ironsworn::impacts",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                entity = new { type = "string", description = ToolDescriptions.ImpactEntity + " Leave it out to read all impacts." }
            },
            required = new[] { "campaign" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var entity = arguments.OptionalStateId("entity");

        _logger.LogDebug("Executing get_impacts: campaign={Campaign}, entity={Entity}", campaign.Value, entity?.Value);

        var impacts = entity is null
            ? await _impacts.GetImpacts(campaign)
            : await _impacts.GetImpacts(campaign, entity);

        return impacts.Map(
            onSuccess: all => McpJson.Serialize(new { impacts = all.Select(ImpactRecord.From) }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
