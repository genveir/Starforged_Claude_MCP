using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Server.Models;

/// <summary>
/// A meter as the meter tools return it. Max is left out for a meter unbounded above, and Clamped is
/// present only when a change did not fit the meter's range.
/// </summary>
public record MeterRecord(
    string Name,
    int Value,
    int Min,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Max,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Clamped)
{
    public static MeterRecord From(Meter meter) =>
        new(meter.Name.Value, meter.Value, meter.Min, meter.Max, Clamped: null);

    public static MeterRecord From(MeterChange change) =>
        From(change.Meter) with { Clamped = change.Clamped == 0 ? null : change.Clamped };
}
