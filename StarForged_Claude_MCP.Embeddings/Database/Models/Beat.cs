using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Database.Models;

public class Beat
{
    [JsonIgnore]
    public int Id { get; set; }

    [JsonIgnore]
    public int SessionNumber { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BeatNumber { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Version { get; set; }

    public string Content { get; set; } = string.Empty;

    public int Sequence { get; set; }
}
