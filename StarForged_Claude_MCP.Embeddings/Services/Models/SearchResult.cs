namespace StarForged_Claude_MCP.Embeddings.Services.Models
{
    public record SearchResult(string Text, float SimilarityScore);

    internal record SimilarityResult(int Id, float SimilarityScore);
}
