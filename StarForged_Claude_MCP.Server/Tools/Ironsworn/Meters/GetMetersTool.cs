using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Meters;

public class GetMetersTool : ITool
{
    private readonly IMeterService _meters;
    private readonly ILogger<GetMetersTool> _logger;

    public GetMetersTool(IMeterService meters, ILogger<GetMetersTool> logger)
    {
        _meters = meters;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_meters",
        Description = "Reads the campaign's meters: one meter by name, or all of them when name is left out. A meter is returned as {\"name\", \"value\", \"min\", \"max\"}, with max left out when the meter has no upper bound; all meters come back as {\"meters\": [...]}, ordered by name. tag::ironsworn::meters",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                name = new { type = "string", description = ToolDescriptions.MeterName + " Leave it out to read all meters." }
            },
            required = new[] { "campaign" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var name = arguments.OptionalStateId("name");

        _logger.LogDebug("Executing get_meters: campaign={Campaign}, name={Name}", campaign.Value, name?.Value);

        if (name is null)
        {
            var meters = await _meters.GetMeters(campaign);

            return meters.Map(
                onSuccess: all => McpJson.Serialize(new { meters = all.Select(MeterRecord.From) }),
                onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
        }

        var meter = await _meters.GetMeter(campaign, name);

        return meter.Map(
            onSuccess: found => McpJson.Serialize(MeterRecord.From(found)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
