using System.Text.Json.Serialization;

namespace StarForged_Claude_MCP.Embeddings.Services.Models
{
    public record SearchResult(string Text, float SimilarityScore, [property: JsonIgnore] int Id)
    {
        public string BriefSummary
        {
            get
            {
                var colonIndex = Text.IndexOf(':');
                string breadcrumbs = colonIndex < 0 ? string.Empty : Text[..colonIndex];
                string content = colonIndex < 0 ? Text : Text[(colonIndex + 1)..].TrimStart();

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

                return colonIndex < 0 ? contentSummary : $"{breadcrumbs}: {contentSummary}";
            }
        }
    }

    internal record SimilarityResult(int Id, float SimilarityScore);
}
