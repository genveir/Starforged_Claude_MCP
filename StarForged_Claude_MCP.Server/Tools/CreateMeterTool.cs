using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class CreateMeterTool : ITool
{
    private readonly IMeterService _meters;
    private readonly ILogger<CreateMeterTool> _logger;

    public CreateMeterTool(IMeterService meters, ILogger<CreateMeterTool> logger)
    {
        _meters = meters;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "create_meter",
        Description = "Creates a meter in the campaign, with a range and a starting value. Returns the meter as {\"name\", \"value\", \"min\", \"max\"}, with max left out when the meter has no upper bound; when the starting value had to be clamped to the range, \"clamped\" gives the amount that did not fit. tag::ironsworn",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                name = new { type = "string", description = ToolDescriptions.MeterName },
                min = new { type = "integer", description = "The lowest value the meter can hold." },
                max = new { type = "integer", description = "Optional. The highest value the meter can hold; leave it out for no upper bound." },
                value = new { type = "integer", description = "The starting value, clamped into the range." }
            },
            required = new[] { "campaign", "name", "min", "value" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var name = arguments.RequireStateId("name");
        var min = arguments.RequireInt("min");
        var max = arguments.OptionalInt("max");
        var value = arguments.RequireInt("value");

        _logger.LogDebug(
            "Executing create_meter: campaign={Campaign}, name={Name}, min={Min}, max={Max}, value={Value}",
            campaign.Value, name.Value, min, max, value);

        var created = await _meters.CreateMeter(campaign, name, min, max, value);

        return created.Map(
            onSuccess: change => McpJson.Serialize(MeterRecord.From(change)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
