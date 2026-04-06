using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Database.Models
{
    public class DocumentResult
    {
        public string Content { get; set; } = string.Empty;

        public int Sequence { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BeatNumber { get; set; }
    }
}
