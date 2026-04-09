using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.ConsoleAccess.Search;

public class Searcher
{
    private readonly ISearchService searchService;

    public Searcher(ISearchService searchService)
    {
        this.searchService = searchService;
    }

    public async Task Search(SearchOptions options)
    {
        var results = await searchService.Search(options.SearchString, options.TopK);

        foreach (var result in results)
        {
            switch (options.OutputType)
            {
                case SearchOutputType.Full:
                    OutputFullResult(result);
                    break;
                case SearchOutputType.Brief:
                    OutputBriefResult(result);
                    break;
                default:
                    throw new ArgumentException($"Unexpected output type: {options.OutputType}");
            }
        }
    }

    private static void OutputFullResult(SearchResult result)
    {
        Console.WriteLine($"[Id: {result.Id}, Score: {result.SimilarityScore}]");
        Console.WriteLine(result.Text);
        Console.WriteLine();
    }

    private static void OutputBriefResult(SearchResult result)
    {
        var colonIndex = result.Text.IndexOf(':');

        string breadcrumbs = colonIndex < 0 ? string.Empty : result.Text[..colonIndex];
        string content = colonIndex < 0 ? result.Text : result.Text[(colonIndex + 1)..].TrimStart();

        var sentenceEnd = content.IndexOfAny(['.', '!', '?', '\n']);
        string contentSummary;
        if (sentenceEnd < 0)
        {
            contentSummary = content;
        }
        else
        {
            contentSummary = content[..(sentenceEnd + 1)];
            if (contentSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 10)
            {
                var secondEnd = content.IndexOfAny(['.', '!', '?', '\n'], sentenceEnd + 1);
                if (secondEnd >= 0) contentSummary = content[..(secondEnd + 1)];
            }
        }

        Console.WriteLine($"[Id: {result.Id}, Score: {result.SimilarityScore}] {breadcrumbs}: {contentSummary}");
    }
}
