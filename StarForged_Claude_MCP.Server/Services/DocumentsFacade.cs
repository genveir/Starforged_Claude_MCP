using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class DocumentsFacade : IDocumentsFacade
{
    private readonly DbInterface _dbInterface;
    private readonly IDocumentProcessingService _documentProcessing;

    public DocumentsFacade(DbInterface dbInterface, IDocumentProcessingService documentProcessing)
    {
        _dbInterface = dbInterface;
        _documentProcessing = documentProcessing;
    }

    public async Task<bool> AddDocumentAsync(string category, string filename, string content, string? summary, bool indexed)
    {
        if (await _dbInterface.GetDocument(category, filename) != null) return false;

        var id = await _dbInterface.StoreDocument(category, filename, content, NullIfBlank(summary));

        if (indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, id, DocumentProcessorToUse.Markdown);
        }

        return true;
    }

    public async Task<bool> UpdateDocumentAsync(string category, string filename, string content, string? summary) =>
        await WriteAsync(category, filename, summary, rewrite: _ => content);

    public async Task<bool> ReplaceSectionAsync(string category, string filename, string section, string text, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.ReplaceSection(existing.Content, section, text));

    public async Task<bool> AppendAsync(string category, string filename, string? section, string text, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.AppendToSection(existing.Content, section, text));

    public async Task<bool> DeleteSectionAsync(string category, string filename, string section, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.DeleteSection(existing.Content, section));

    public async Task<bool> IndexDocumentAsync(string category, string filename)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        await _documentProcessing.IndexDocumentAsync(existing.Content, existing.Id, DocumentProcessorToUse.Markdown);
        return true;
    }

    public async Task<bool> DeindexDocumentAsync(string category, string filename)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        await _documentProcessing.RemoveIndexForDocumentAsync(existing.Id);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(string category, string filename)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        await _dbInterface.DeleteDocument(existing.Id);
        return true;
    }

    public async Task<Document?> GetDocumentAsync(string category, string filename) =>
        await _dbInterface.GetDocument(category, filename);

    public async Task<DocumentIndexEntry?> GetDocumentSummaryAsync(string category, string filename) =>
        await _dbInterface.GetDocumentSummary(category, filename);

    public async Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(string category) =>
        await _dbInterface.GetDocumentIndex(category);

    public async Task<TextSearchResult?> FindTextAsync(string category, string text, bool wholeWord, string? filename)
    {
        if (filename != null && await _dbInterface.GetDocumentSummary(category, filename) == null) return null;

        var candidates = await _dbInterface.FindDocumentsContaining(category, text, filename);
        return DocumentTextSearch.Search(candidates, text, wholeWord);
    }

    public async Task<List<Beat>> GetCanonicalBeatsAsync(string category, int sessionNumber) =>
        CanonicalBeats.Select(await _dbInterface.GetBeatsForSession(category, sessionNumber));

    public async Task<List<string>> GetSubcategoriesAsync(string category) =>
        await _dbInterface.GetCategoriesUnder(category);

    public async Task<List<string>> GetAncestorsHoldingDocumentsAsync(string category) =>
        await _dbInterface.GetAncestorsHoldingDocuments(category);

    /// <summary>
    /// The one path every content write takes: rewrite the content, keep the summary unless this
    /// write carries one of its own, and rebuild the index only for a document that already had one.
    /// Whether a document is indexed is <see cref="IndexDocumentAsync"/>'s business, not an edit's.
    /// </summary>
    private async Task<bool> WriteAsync(string category, string filename, string? summary, Func<Document, string> rewrite)
    {
        var existing = await _dbInterface.GetDocument(category, filename);
        if (existing == null) return false;

        var content = rewrite(existing);

        await _dbInterface.UpdateDocument(existing.Id, content, ResolveSummary(existing.Summary, summary));

        if (existing.Indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
        }

        return true;
    }

    private static string? ResolveSummary(string? existingSummary, string? summary) =>
        summary == null ? existingSummary : NullIfBlank(summary);

    private static string? NullIfBlank(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary;
}
