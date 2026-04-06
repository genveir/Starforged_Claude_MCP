using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing
{
    public class UnchunkableFlatTextPreprocessor : IDocumentPreprocessor
    {
        private readonly ILogger<UnchunkableFlatTextPreprocessor> _logger;
        private const int MaxTokens = 512;

        public UnchunkableFlatTextPreprocessor(ILogger<UnchunkableFlatTextPreprocessor> logger)
        {
            _logger = logger;
        }

        public PreprocessedText Process(string text)
        {
            text = text.Trim();

            var tokens = Tokenizer.Tokenize(text);

            if (tokens.Length == 0)
            {
                _logger.LogWarning("Input text resulted in zero tokens after tokenization. Returning empty PreprocessedText.");
                return new PreprocessedText([]);
            }

            if (tokens.Length > MaxTokens)
            {
                _logger.LogWarning("Input text exceeds maximum token limit of {MaxTokens}. Truncating input.", MaxTokens);
                tokens = tokens[..MaxTokens];
            }

            return new PreprocessedText([new Chunk(tokens, text)]);
        }
    }
}
