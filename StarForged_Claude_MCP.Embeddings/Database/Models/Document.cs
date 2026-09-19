using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Database.Models;

public class Document
{
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Filename { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    public bool Indexed { get; set; }
}
