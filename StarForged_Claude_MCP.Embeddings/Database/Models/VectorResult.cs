namespace StarForged_Claude_MCP.Embeddings.Database.Models;

public class VectorResult
{
    public int Id { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}
