using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Server.Models;

/// <summary>
/// A track as the track tools return it, with its kind and rank in lowercase. Clamped is present only when a
/// change did not fit 0 to 40 ticks.
/// </summary>
public record TrackRecord(
    string Track,
    string Kind,
    string Description,
    string Rank,
    int Ticks,
    int Boxes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Clamped)
{
    public static TrackRecord From(Track track) =>
        new(track.Id.Value,
            track.Id.Kind.ToString().ToLowerInvariant(),
            track.Description,
            track.Rank.ToString().ToLowerInvariant(),
            track.Ticks,
            track.Boxes,
            Clamped: null);

    public static TrackRecord From(TrackChange change) =>
        From(change.Track) with { Clamped = change.Clamped == 0 ? null : change.Clamped };
}
