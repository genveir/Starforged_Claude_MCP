using BERTTokenizers;
using Microsoft.Extensions.Logging;

namespace StarForged_Claude_MCP.Embeddings.Services;

public class MarkdownChunker
{
    private readonly BertUncasedLargeTokenizer _tokenizer;
    private readonly ILogger<MarkdownChunker> _logger;
    private const int MaxTokens = 512;

    public MarkdownChunker(BertUncasedLargeTokenizer tokenizer, ILogger<MarkdownChunker> logger)
    {
        _tokenizer = tokenizer;
        _logger = logger;
    }

    public List<string> ChunkBySection(string markdown)
    {
        var chunks = new List<string>();
        var lines = markdown.Split('\n');
        var currentChunk = new List<string>();
        var headerHierarchy = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();

            if ((trimmedLine.StartsWith('#') || trimmedLine.StartsWith("**")) && currentChunk.Count > 0)
            {
                var chunkWithContext = new List<string>(headerHierarchy);
                chunkWithContext.AddRange(currentChunk);
                chunks.Add(string.Join('\n', chunkWithContext).Trim());
                currentChunk.Clear();
            }

            if (trimmedLine.StartsWith('#'))
            {
                var level = trimmedLine.TakeWhile(c => c == '#').Count();

                while (headerHierarchy.Count >= level)
                {
                    headerHierarchy.RemoveAt(headerHierarchy.Count - 1);
                }

                headerHierarchy.Add(line);
            }

            currentChunk.Add(line);
        }

        if (currentChunk.Count > 0)
        {
            var chunkWithContext = new List<string>(headerHierarchy);
            chunkWithContext.AddRange(currentChunk);
            chunks.Add(string.Join('\n', chunkWithContext).Trim());
        }

        var validatedChunks = chunks
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .SelectMany(c =>
            {
                ValidateChunk(c);
                return SplitChunkIfNeeded(c);
            })
            .ToList();

        return validatedChunks;
    }

    private void ValidateChunk(string chunk)
    {
        var tokenCount = CountTokens(chunk);

        if (tokenCount > MaxTokens)
        {
            _logger.LogWarning("Chunk exceeds {MaxTokens} tokens ({ActualTokens}), will attempt to split", MaxTokens, tokenCount);
        }
    }

    private List<string> SplitChunkIfNeeded(string chunk)
    {
        var tokenCount = CountTokens(chunk);

        if (tokenCount <= MaxTokens)
        {
            return [chunk];
        }

        return SplitChunkByParagraphs(chunk);
    }

    private List<string> SplitChunkByParagraphs(string chunk)
    {
        var paragraphs = chunk.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        if (paragraphs.Length == 1)
        {
            var tokenCount = CountTokens(chunk);
            _logger.LogWarning("Single paragraph chunk with {TokenCount} tokens cannot be split further, will be truncated by tokenizer", tokenCount);
            return [chunk];
        }

        var result = new List<string>();
        var currentParagraphChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokens = CountTokens(paragraph);

            if (paragraphTokens > MaxTokens)
            {
                _logger.LogWarning("Single paragraph exceeds {MaxTokens} tokens ({ActualTokens}), will be truncated", MaxTokens, paragraphTokens);

                if (currentParagraphChunk.Count > 0)
                {
                    result.Add(string.Join("\n\n", currentParagraphChunk));
                    currentParagraphChunk.Clear();
                    currentTokenCount = 0;
                }

                result.Add(paragraph);
                continue;
            }

            if (currentTokenCount + paragraphTokens > MaxTokens)
            {
                result.Add(string.Join("\n\n", currentParagraphChunk));
                currentParagraphChunk.Clear();
                currentTokenCount = 0;
            }

            currentParagraphChunk.Add(paragraph);
            currentTokenCount += paragraphTokens;
        }

        if (currentParagraphChunk.Count > 0)
        {
            result.Add(string.Join("\n\n", currentParagraphChunk));
        }

        return result;
    }

    private int CountTokens(string text)
    {
        var tokens = _tokenizer.Encode(MaxTokens, text);
        return tokens.Count;
    }
}
