using BERTTokenizers.Base;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using System.Globalization;
using System.Text;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing
{
    internal static class Tokenizer
    {
        private sealed class TokenizerWithPath(string path) : UncasedTokenizer(path);

        private static readonly string vocabularyPath =
            Path.Combine(AppContext.BaseDirectory, "Vocabularies", "base_uncased_large.txt");

        private static readonly UncasedTokenizer tokenizer = new TokenizerWithPath(vocabularyPath);

        private static readonly HashSet<char> tokenizableCharacters = LoadTokenizableCharacters();

        public static Token[] Tokenize(string text) => GetTokens(text, 512);

        public static Token[] GetTokensForCount(string text)
        {
            var tokens = GetTokens(text, 8000);

            return tokens.Where(t => t.AttentionMask != 0).ToArray();
        }

        private static Token[] GetTokens(string text, int length)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');
            text = MakeTokenizable(text);

            var tokens = tokenizer.Encode(length, text);

            return [.. tokens
                .Select(x => new Token(x.InputIds, x.AttentionMask, x.TokenTypeIds))];
        }

        /// <summary>
        /// Strips accents, as BERT uncased expects, and replaces every character the vocabulary cannot tokenize
        /// with a space. The library never gives up on a word holding such a character: it loops forever,
        /// growing its token list, so 'Pokémon' or 'Smørrebrød' would hang indexing.
        /// </summary>
        internal static string MakeTokenizable(string text)
        {
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;

                builder.Append(char.IsWhiteSpace(character) || tokenizableCharacters.Contains(char.ToLowerInvariant(character))
                    ? character
                    : ' ');
            }

            return builder.ToString();
        }

        /// <summary>
        /// A character can be tokenized when the vocabulary holds it both on its own, to start a word, and
        /// as a '##' continuation, to appear inside one.
        /// </summary>
        private static HashSet<char> LoadTokenizableCharacters()
        {
            var entries = File.ReadLines(vocabularyPath).ToHashSet();

            return [.. entries
                .Where(entry => entry.Length == 1 && entries.Contains($"##{entry}"))
                .Select(entry => entry[0])];
        }
    }
}
