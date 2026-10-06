using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

/// <summary>A track's kind and name, written as one id with a dot between them, e.g. 'vow.handle-the-plantation'.</summary>
public record TrackId(TrackKind Kind, string Name)
{
    public string Value => $"{Kind.ToString().ToLowerInvariant()}.{Name}";

    /// <summary>
    /// Splits an id into its kind and name. Fails with <see cref="ErrorCode.Track_Id_Needs_Kind"/> when the id
    /// has no dot, and with <see cref="ErrorCode.Track_Kind_Unknown"/> when what precedes it is no kind, in any casing.
    /// </summary>
    public static Result<TrackId, ErrorCode> From(StateTrackingId id)
    {
        var dot = id.Value.IndexOf('.');
        if (dot < 0)
            return Result<TrackId, ErrorCode>.Fail(ErrorCode.Track_Id_Needs_Kind);

        // Checked by name first: Enum.Parse alone would also take a number such as '1' for a kind.
        var kind = id.Value[..dot];
        if (!Enum.GetNames<TrackKind>().Contains(kind, StringComparer.OrdinalIgnoreCase))
            return Result<TrackId, ErrorCode>.Fail(ErrorCode.Track_Kind_Unknown);

        return Result<TrackId, ErrorCode>.Succeed(
            new TrackId(Enum.Parse<TrackKind>(kind, ignoreCase: true), id.Value[(dot + 1)..]));
    }
}
