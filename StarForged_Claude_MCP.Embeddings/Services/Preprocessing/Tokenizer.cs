using BERTTokenizers;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing
{
    internal static class Tokenizer
    {
        private static readonly BertUncasedLargeTokenizer tokenizer = new();

        public static Token[] Tokenize(string text)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            var tokens = tokenizer.Encode(8000, text);

            return [.. tokens
                .Where(x => x.AttentionMask != 0)
                .Select(x => new Token(x.InputIds, x.AttentionMask, x.TokenTypeIds))];
        }
    }
}
