using StarForged_Claude_MCP.Database.DomainTypes;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Server.Services;

public class DocumentsFacade : IDocumentsFacade
{
    private readonly DocumentsRepository _documents;
    private readonly IDocumentProcessingService _documentProcessing;

    public DocumentsFacade(DocumentsRepository documents, IDocumentProcessingService documentProcessing)
    {
        _documents = documents;
        _documentProcessing = documentProcessing;
    }

    public async Task<bool> AddDocumentAsync(Category category, string filename, string content, string? summary, bool indexed)
    {
        if (await _documents.GetDocument(category.ToCategoryPath(), filename) != null) return false;

        var id = await _documents.StoreDocument(category.ToCategoryPath(), filename, content, NullIfBlank(summary));

        if (indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, id, DocumentProcessorToUse.Markdown);
        }

        return true;
    }

    public async Task<bool> UpdateDocumentAsync(Category category, string filename, string content, string? summary, bool? indexed) =>
        await WriteAsync(category, filename, summary, rewrite: _ => content, indexed);

    public async Task<bool> ReplaceSectionAsync(Category category, string filename, string section, string text, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.ReplaceSection(existing.Content, section, text));

    public async Task<int?> ReplaceSectionTextAsync(Category category, string filename, string section, string oldText, string newText, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.ReplaceTextInSection(existing.Content, section, oldText, newText));

    public async Task<bool> AppendAsync(Category category, string filename, string? section, string text, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.AppendToSection(existing.Content, section, text));

    public async Task<bool> DeleteSectionAsync(Category category, string filename, string section, string? summary) =>
        await WriteAsync(category, filename, summary,
            rewrite: existing => MarkdownSectionEditor.DeleteSection(existing.Content, section));

    public async Task<bool> DeleteDocumentAsync(Category category, string filename)
    {
        var existing = await _documents.GetDocument(category.ToCategoryPath(), filename);
        if (existing == null) return false;

        await _documents.DeleteDocument(existing.Id);
        return true;
    }

    public async Task<Document?> GetDocumentAsync(Category category, string filename) =>
        await _documents.GetDocument(category.ToCategoryPath(), filename);

    public async Task<DocumentIndexEntry?> GetDocumentSummaryAsync(Category category, string filename) =>
        await _documents.GetDocumentSummary(category.ToCategoryPath(), filename);

    public async Task<List<DocumentIndexEntry>> GetDocumentIndexAsync(Category category) =>
        await _documents.GetDocumentIndex(category.ToCategoryPath());

    public async Task<TextSearchResult?> FindTextAsync(Category category, string text, bool wholeWord, string? filename)
    {
        if (filename != null && await _documents.GetDocumentSummary(category.ToCategoryPath(), filename) == null) return null;

        var candidates = await _documents.FindDocumentsContaining(category.ToCategoryPath(), text, filename);
        return DocumentTextSearch.Search(candidates, text, wholeWord);
    }

    public async Task<List<string>> GetSubcategoriesAsync(Category category) =>
        await _documents.GetCategoriesUnder(category.ToCategoryPath());

    public async Task<List<string>> GetAncestorsHoldingDocumentsAsync(Category category) =>
        await _documents.GetAncestorsHoldingDocuments(category.ToCategoryPath());

    /// <summary>
    /// The one path every content write takes: rewrite the content, keep the summary unless this
    /// write carries one of its own, and rebuild the index only for a document that already had one.
    /// Whether a document is indexed is left alone unless indexed is given, which only a full
    /// <see cref="UpdateDocumentAsync"/> does; a section edit is not a decision about indexing.
    /// </summary>
    private async Task<bool> WriteAsync(Category category, string filename, string? summary, Func<Document, string> rewrite, bool? indexed = null)
    {
        var existing = await _documents.GetDocument(category.ToCategoryPath(), filename);
        if (existing == null) return false;

        var content = rewrite(existing);

        await _documents.UpdateDocument(existing.Id, content, ResolveSummary(existing.Summary, summary));

        if (indexed ?? existing.Indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
        }
        else if (existing.Indexed)
        {
            await _documentProcessing.RemoveIndexForDocumentAsync(existing.Id);
        }

        return true;
    }

    /// <summary>
    /// Same write path as <see cref="WriteAsync(string,string,string?,Func{Document,string})"/>, for a
    /// rewrite that also has to report something about the change it made, such as a replacement count.
    /// </summary>
    private async Task<TResult?> WriteAsync<TResult>(Category category, string filename, string? summary, Func<Document, (string Content, TResult Result)> rewrite)
        where TResult : struct
    {
        var existing = await _documents.GetDocument(category.ToCategoryPath(), filename);
        if (existing == null) return null;

        var (content, result) = rewrite(existing);

        await _documents.UpdateDocument(existing.Id, content, ResolveSummary(existing.Summary, summary));

        if (existing.Indexed)
        {
            await _documentProcessing.IndexDocumentAsync(content, existing.Id, DocumentProcessorToUse.Markdown);
        }

        return result;
    }

    private static string? ResolveSummary(string? existingSummary, string? summary) =>
        summary == null ? existingSummary : NullIfBlank(summary);

    private static string? NullIfBlank(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary;
}
