using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing
{
    public interface IDocumentPreprocessor
    {
        PreprocessedText Process(string text);
    }
}
