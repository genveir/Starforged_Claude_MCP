using BERTTokenizers.Base;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing
{
    internal static class Tokenizer
    {
        private sealed class TokenizerWithPath(string path) : UncasedTokenizer(path);

        private static readonly UncasedTokenizer tokenizer = new TokenizerWithPath(
            Path.Combine(AppContext.BaseDirectory, "Vocabularies", "base_uncased_large.txt"));

        public static Token[] Tokenize(string text) => GetTokens(text, 512);

        public static Token[] GetTokensForCount(string text)
        {
            var tokens = GetTokens(text, 8000);

            return tokens.Where(t => t.AttentionMask != 0).ToArray();
        }

        private static Token[] GetTokens(string text, int length)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            var tokens = tokenizer.Encode(length, text);

            return [.. tokens
                .Select(x => new Token(x.InputIds, x.AttentionMask, x.TokenTypeIds))];
        }
    }
}