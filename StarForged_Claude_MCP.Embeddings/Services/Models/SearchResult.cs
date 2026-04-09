using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Services.Models
{
    public record SearchResult(string Text, float SimilarityScore, [property: JsonIgnore] int Id);

    internal record SimilarityResult(int Id, float SimilarityScore);
}
