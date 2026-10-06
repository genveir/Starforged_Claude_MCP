namespace StarForged_Claude_MCP.Database.Models;

public class TrackRow
{
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Rank { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Ticks { get; set; }
}
