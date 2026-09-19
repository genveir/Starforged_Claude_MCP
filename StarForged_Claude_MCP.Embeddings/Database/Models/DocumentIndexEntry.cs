using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Database.Models;

public class DocumentIndexEntry
{
    public string Filename { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    public bool Indexed { get; set; }
}
