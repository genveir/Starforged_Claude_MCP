namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

public enum Rank
{
    Troublesome,
    Dangerous,
    Formidable,
    Extreme,
    Epic
}

public static class RankExtensions
{
    /// <summary>The ticks one mark of progress adds to a track of this rank.</summary>
    public static int TicksPerMark(this Rank rank) => rank switch
    {
        Rank.Troublesome => 12,
        Rank.Dangerous => 8,
        Rank.Formidable => 4,
        Rank.Extreme => 2,
        Rank.Epic => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, message: null)
    };
}
