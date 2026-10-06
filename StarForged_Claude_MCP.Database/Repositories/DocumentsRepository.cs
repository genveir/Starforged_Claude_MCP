using Dapper;
using StarForged_Claude_MCP.Database.DomainTypes;
using StarForged_Claude_MCP.Database.Models;

namespace StarForged_Claude_MCP.Database.Repositories;

public class DocumentsRepository
{
    private readonly DbConnectionFactory _connections;

    public DocumentsRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    private const string IndexedColumn =
        "cast(case when exists (select 1 from Embeddings e where e.DocumentId = d.Id) then 1 else 0 end as bit) as Indexed";

    public async Task<int> StoreDocument(CategoryPath category, string filename, string content, string? summary)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleAsync<int>(
            """
            insert into Documents (Category, Filename, Content, Summary)
            output inserted.Id
            values (@Category, @Filename, @Content, @Summary)
            """,
            new { Category = category.Value, Filename = filename, Content = content, Summary = summary });
    }

    public async Task<Document?> GetDocument(CategoryPath category, string filename)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<Document>(
            $"select d.Id, d.Category, d.Filename, d.Content, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category and d.Filename = @Filename",
            new { Category = category.Value, Filename = filename });
    }

    public async Task<DocumentIndexEntry?> GetDocumentSummary(CategoryPath category, string filename)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<DocumentIndexEntry>(
            $"select d.Filename, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category and d.Filename = @Filename",
            new { Category = category.Value, Filename = filename });
    }

    public async Task<List<DocumentIndexEntry>> GetDocumentIndex(CategoryPath category)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<DocumentIndexEntry>(
            $"select d.Filename, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category order by d.Filename",
            new { Category = category.Value });
        return results.ToList();
    }

    /// <summary>
    /// Documents in <paramref name="category"/> or any category under it that may contain every word of
    /// <paramref name="text"/>, in order and ignoring case. This is a coarse filter: the words may be separated
    /// by anything, not just whitespace, so the caller still has to find the actual matches in the content
    /// that comes back.
    /// </summary>
    public async Task<List<Document>> FindDocumentsContaining(CategoryPath category, string text, string? filename)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(EscapeLike);
        var pattern = $"%{string.Join("%", words)}%";

        var parameters = category.InScopeParameters();
        parameters.Add("Filename", filename);
        parameters.Add("Pattern", pattern);

        using var connection = _connections.Create();
        var results = await connection.QueryAsync<Document>(
            $"""
            select d.Id, d.Category, d.Filename, d.Content, d.Summary
            from Documents d
            where {CategoryPath.InScopeSql}
              and (@Filename is null or d.Filename = @Filename)
              and d.Content collate Latin1_General_100_CI_AS like @Pattern escape '\'
            """,
            parameters);
        return results.ToList();
    }

    private static string EscapeLike(string text) => text
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_")
        .Replace("[", @"\[");

    public async Task<List<string>> GetCategories()
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<string>(
            "select distinct Category from Documents order by Category");
        return results.ToList();
    }

    /// <summary>
    /// The categories holding documents anywhere under <paramref name="category"/>, not counting itself.
    /// Empty for a leaf, and for a category that does not exist yet.
    /// </summary>
    public async Task<List<string>> GetCategoriesUnder(CategoryPath category)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<string>(
            "select distinct d.Category from Documents d where left(d.Category, len(@Prefix)) = @Prefix order by d.Category",
            new { Prefix = category.DescendantPrefix() });
        return results.ToList();
    }

    /// <summary>
    /// The categories above <paramref name="category"/> that hold documents themselves, nearest the root first.
    /// </summary>
    public async Task<List<string>> GetAncestorsHoldingDocuments(CategoryPath category)
    {
        var ancestors = category.Ancestors();
        if (ancestors.Count == 0) return [];

        using var connection = _connections.Create();
        var results = await connection.QueryAsync<string>(
            "select distinct d.Category from Documents d where d.Category in @Ancestors",
            new { Ancestors = ancestors });
        return results.OrderBy(ancestor => ancestor.Length).ToList();
    }

    public async Task UpdateDocument(int id, string content, string? summary)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            "update Documents set Content = @Content, Summary = @Summary where Id = @Id",
            new { Id = id, Content = content, Summary = summary });
    }

    public async Task DeleteDocument(int id)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync("delete from Documents where Id = @Id", new { Id = id });
    }

    public async Task DeleteAllDocuments()
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync("delete from Documents");
    }
}
