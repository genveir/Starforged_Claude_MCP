namespace StarForged_Claude_MCP.Embeddings.Database.Models;

public class TextResult
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
}
