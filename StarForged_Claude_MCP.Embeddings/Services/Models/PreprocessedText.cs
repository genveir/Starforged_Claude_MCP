namespace StarForged_Claude_MCP.Embeddings.Services.Models
{
    public record Token(long InputIds, long AttentionMask, long TokenTypeIds);

    public record PreprocessedText(Chunk[] Chunks);

    public record Chunk(Token[] Tokens, string Text);
}
