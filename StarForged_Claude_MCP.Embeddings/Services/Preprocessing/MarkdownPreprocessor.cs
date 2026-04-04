using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

public class MarkdownPreprocessor : IDocumentPreprocessor
{
    private const int MaxTokens = 512;

    public PreprocessedText Process(string text)
    {
        var processorChunks = PreprocessMarkdown(text);

        List<Chunk> chunks = [.. processorChunks.Select(pc =>
            new Chunk(
                Tokens: pc.Tokens,
                EmbedText: pc.Breadcrumb + "\n" + pc.Text,
                DisplayText: pc.Text))];

        return new PreprocessedText(Chunks: [.. chunks]);
    }

    private static List<ProcessorChunk> PreprocessMarkdown(string text)
    {
        var lines = text.Split(['\n', '\r']);

        var splitByHeaders = ChunkByHighLevelHeaders(lines);

        var trimmed = TrimLines(splitByHeaders);

        List<ProcessorChunk> wellSizedChunks = [];
        for (int n = 0; n < trimmed.Count; n++)
        {
            var chunk = trimmed[n];
            if (chunk.Tokens.Length > MaxTokens)
            {
                wellSizedChunks.AddRange(RechunkHighLevelChunk(chunk));
            }
            else
            {
                wellSizedChunks.Add(chunk);
            }
        }

        return StripEmptyChunks(wellSizedChunks);
    }

    private static List<ProcessorChunk> StripEmptyChunks(List<ProcessorChunk> processorChunks)
    {
        return [.. processorChunks.Where(pc => pc.Tokens.Length > 0)];
    }

    private static List<ProcessorChunk> ChunkByHighLevelHeaders(string[] lines)
    {
        // single pass, split on # and ##, put header names in breadcrumb.
        List<ProcessorChunk> processorChunks = [];

        List<string> chunkLines = [];
        string?[] breadcrumbParts = new string?[2];
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("# "))
            {
                if (chunkLines.Count > 0)
                {
                    processorChunks.Add(CreateProcessorChunk(chunkLines, breadcrumbParts));
                    chunkLines.Clear();
                }

                breadcrumbParts[0] = line;
                breadcrumbParts[1] = null;
            }

            else if (line.StartsWith("## "))
            {
                if (chunkLines.Count > 0)
                {
                    processorChunks.Add(CreateProcessorChunk(chunkLines, breadcrumbParts));
                    chunkLines.Clear();
                }

                breadcrumbParts[1] = line;
            }
            else
            {
                chunkLines.Add(line);
            }
        }

        if (chunkLines.Count > 0)
        {
            processorChunks.Add(CreateProcessorChunk(chunkLines, breadcrumbParts));
        }

        return processorChunks;
    }

    private static List<ProcessorChunk> RechunkHighLevelChunk(ProcessorChunk processorChunk)
    {
        var splitByLowLevelHeaders = ChunkByLowLevelHeaders(processorChunk);

        var trimmed = TrimLines(splitByLowLevelHeaders);

        List<ProcessorChunk> wellSizedChunks = [];
        for (int n = 0; n < trimmed.Count; n++)
        {
            var chunk = trimmed[n];
            if (chunk.Tokens.Length > MaxTokens)
            {
                wellSizedChunks.AddRange(RechunkLowLevelChunk(chunk));
            }
            else
            {
                wellSizedChunks.Add(chunk);
            }
        }

        return wellSizedChunks;
    }

    private static List<ProcessorChunk> ChunkByLowLevelHeaders(ProcessorChunk processorChunk)
    {
        return SplitByHeader(processorChunk, "### ");
    }

    private static ProcessorChunk CreateProcessorChunkWithOptionalBreadcrumb(ProcessorChunk parent, List<string> chunkLines, string currentHeader)
    {
        var tentative = CreateProcessorChunk(chunkLines, parent.BreadcrumbParts);

        if (tentative.Tokens.Length < MaxTokens)
        {
            return new ProcessorChunk(
                lines: [.. chunkLines],
                breadcrumbParts: parent.BreadcrumbParts);
        }
        else
        {
            return new ProcessorChunk(
                lines: [.. chunkLines],
                breadcrumbParts: [.. parent.BreadcrumbParts, currentHeader]);
        }
    }

    private static List<ProcessorChunk> RechunkLowLevelChunk(ProcessorChunk processorChunk)
    {
        var splitByBoldHeader = ChunkByBoldHeader(processorChunk);

        var trimmed = TrimLines(splitByBoldHeader);

        List<ProcessorChunk> wellSizedChunks = [];
        for (int n = 0; n < trimmed.Count; n++)
        {
            var chunk = trimmed[n];
            if (chunk.Tokens.Length > MaxTokens)
            {
                wellSizedChunks.AddRange(ChunkGreedyByLineBreaks(chunk));
            }
            else
            {
                wellSizedChunks.Add(chunk);
            }
        }

        return wellSizedChunks;
    }

    private static List<ProcessorChunk> ChunkByBoldHeader(ProcessorChunk processorChunk)
    {
        return SplitByHeader(processorChunk, "**");
    }

    private static List<ProcessorChunk> SplitByHeader(ProcessorChunk processorChunk, string headerMarker)
    {
        List<ProcessorChunk> processorChunks = [];

        List<string> chunkLines = [];
        string currentHeaderName = string.Empty;

        for (int i = 0; i < processorChunk.Lines.Length; i++)
        {
            var line = processorChunk.Lines[i];

            if (line.StartsWith(headerMarker))
            {
                if (chunkLines.Count > 0)
                {
                    processorChunks.Add(CreateProcessorChunkWithOptionalBreadcrumb(processorChunk, chunkLines, currentHeaderName));
                    chunkLines.Clear();
                }
                currentHeaderName = line;
            }
            else
            {
                chunkLines.Add(line);
            }
        }

        processorChunks.Add(CreateProcessorChunkWithOptionalBreadcrumb(processorChunk, chunkLines, currentHeaderName));

        return processorChunks;
    }

    private static List<ProcessorChunk> ChunkGreedyByLineBreaks(ProcessorChunk processorChunk)
    {
        // split greedily by line breaks. If a paragraph is longer than 512 tokens truncate the tokens to 512.
        var lines = processorChunk.Lines;

        List<string> chunkLines = [];
        ProcessorChunk? current = null;
        List<ProcessorChunk> processorChunks = [];
        for (int n = 0; n < lines.Length; n++)
        {
            var line = lines[n];

            chunkLines.Add(line);

            var tentative = CreateProcessorChunk(chunkLines, processorChunk.BreadcrumbParts);

            if (tentative.Tokens.Length > MaxTokens)
            {
                if (current == null)
                {
                    tentative.TruncateTokensTo(MaxTokens);
                    processorChunks.Add(tentative);
                    chunkLines.Clear();
                }
                else
                {
                    processorChunks.Add(current);
                    current = null;
                    chunkLines.Clear();
                    n--;
                }
            }
            else
            {
                current = tentative;
            }
        }

        if (current != null)
        {
            processorChunks.Add(current);
        }

        return processorChunks;
    }

    private static ProcessorChunk CreateProcessorChunk(List<string> chunkLines, string?[] breadcrumbParts)
    {
        return new ProcessorChunk(
            lines: [.. chunkLines],
            breadcrumbParts: breadcrumbParts);
    }

    private static List<ProcessorChunk> TrimLines(List<ProcessorChunk> processorChunks)
    {
        // trim whitespace, trim horizontal markers at top and bottom
        for (int i = 0; i < processorChunks.Count; i++)
        {
            processorChunks[i] = TrimLines(processorChunks[i]);
        }

        return processorChunks;
    }

    private static ProcessorChunk TrimLines(ProcessorChunk processorChunk)
    {
        int trimFromStart = 0;
        for (int i = 0; i < processorChunk.Lines.Length; i++)
        {
            var line = processorChunk.Lines[i];

            if (IsTrimLine(line))
            {
                trimFromStart++;
                continue;
            }
            else break;
        }

        if (trimFromStart == processorChunk.Lines.Length)
        {
            return new ProcessorChunk(lines: [], processorChunk.BreadcrumbParts);
        }

        int trimFromEnd = 0;
        for (int i = 1; i < processorChunk.Lines.Length; ++i)
        {
            var line = processorChunk.Lines[^i];

            if (IsTrimLine(line))
            {
                trimFromEnd++;
                continue;
            }
            else break;
        }

        return new ProcessorChunk(
            lines: [.. processorChunk.Lines
                .Skip(trimFromStart)
                .Take(processorChunk.Lines.Length - trimFromStart - trimFromEnd)],
            breadcrumbParts: processorChunk.BreadcrumbParts);

        static bool IsTrimLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return true;
            if (line.Trim().StartsWith("---"))
                return true;
            return false;
        }
    }

    private class ProcessorChunk
    {
        public ProcessorChunk(string[] lines, string?[] breadcrumbParts)
        {
            Lines = [.. lines];
            BreadcrumbParts = [.. breadcrumbParts];
        }

        public string[] Lines { get; }
        public string?[] BreadcrumbParts { get; }

        private Token[]? _tokens = null;

        public string Text => string.Join('\n', Lines);
        public string Breadcrumb => string.Join(" > ", BreadcrumbParts.Where(bp => !string.IsNullOrEmpty(bp)));

        public Token[] Tokens
        {
            get
            {
                _tokens ??= Tokenizer.Tokenize(Breadcrumb + '\n' + Text);

                return _tokens;
            }
        }

        public void TruncateTokensTo(int maxValue)
        {
            _tokens = [.. Tokens.Take(maxValue)];
        }
    }
}
