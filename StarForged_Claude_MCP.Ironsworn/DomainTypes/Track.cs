namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

/// <summary>
/// A progress track: ticks between 0 and <see cref="MaxTicks"/>, four to a box. The track keeps its ticks in
/// range and knows how many a mark of its rank is worth; what progress means is left to the game.
/// </summary>
public class Track
{
    public const int MaxTicks = 40;
    public const int TicksPerBox = 4;

    public TrackId Id { get; }
    public string Description { get; }
    public Rank Rank { get; }
    public int Ticks { get; }
    public int Boxes => Ticks / TicksPerBox;

    /// <summary>Rebuilds a track as stored, without validating it: only <see cref="Create"/> lets new tracks in.</summary>
    internal Track(TrackId id, string description, Rank rank, int ticks)
    {
        Id = id;
        Description = description;
        Rank = rank;
        Ticks = ticks;
    }

    public static TrackChange Create(TrackId id, string description, Rank rank, int ticks = 0) =>
        new Track(id, description, rank, ticks: 0).SetTicks(ticks);

    /// <summary>Adds the rank's ticks per mark the given number of times; a negative number removes progress.</summary>
    public TrackChange Mark(int times) => ClampTo(Ticks + times * Rank.TicksPerMark());

    public TrackChange SetTicks(int ticks) => ClampTo(ticks);

    /// <summary>Replaces whichever of description and rank is given. Ticks stay as they are, also on a new rank.</summary>
    public Track Edit(string? description, Rank? rank) =>
        new(Id, description ?? Description, rank ?? Rank, Ticks);

    private TrackChange ClampTo(int target)
    {
        var ticks = Math.Clamp(target, 0, MaxTicks);

        return new TrackChange(new Track(Id, Description, Rank, ticks), Clamped: Math.Abs(target - ticks));
    }
}

/// <summary>The track after a change, and how many ticks of the change did not fit 0 to 40: 0 when all of them did.</summary>
public record TrackChange(Track Track, int Clamped);
