namespace StarForged_Claude_MCP.Database.Models;

public class MeterRow
{
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }

    public int MinValue { get; set; }

    public int? MaxValue { get; set; }
}
