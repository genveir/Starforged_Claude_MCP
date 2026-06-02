using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Database.Models
{
    public class DocumentIndexEntry
    {
        public string SourceDocument { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Summaries { get; set; }
    }
}
