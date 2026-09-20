using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;

namespace StarForged_Claude_MCP.Embeddings.Database;

public class DbInterface
{
    private readonly string _connectionString;

    public DbInterface(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // ---------- Documents ----------

    private const string IndexedColumn =
        "cast(case when exists (select 1 from Embeddings e where e.DocumentId = d.Id) then 1 else 0 end as bit) as Indexed";

    public async Task<int> StoreDocument(string category, string filename, string content, string? summary)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<int>(
            """
            insert into Documents (Category, Filename, Content, Summary)
            output inserted.Id
            values (@Category, @Filename, @Content, @Summary)
            """,
            new { Category = category, Filename = filename, Content = content, Summary = summary });
    }

    public async Task<Document?> GetDocument(string category, string filename)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Document>(
            $"select d.Id, d.Category, d.Filename, d.Content, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category and d.Filename = @Filename",
            new { Category = category, Filename = filename });
    }

    public async Task<DocumentIndexEntry?> GetDocumentSummary(string category, string filename)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<DocumentIndexEntry>(
            $"select d.Filename, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category and d.Filename = @Filename",
            new { Category = category, Filename = filename });
    }

    public async Task<List<DocumentIndexEntry>> GetDocumentIndex(string category)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<DocumentIndexEntry>(
            $"select d.Filename, d.Summary, {IndexedColumn} " +
            "from Documents d where d.Category = @Category order by d.Filename",
            new { Category = category });
        return results.ToList();
    }

    public async Task<List<string>> GetCategories()
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<string>(
            "select distinct Category from Documents order by Category");
        return results.ToList();
    }

    public async Task UpdateDocument(int id, string content, string? summary)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "update Documents set Content = @Content, Summary = @Summary where Id = @Id",
            new { Id = id, Content = content, Summary = summary });
    }

    public async Task DeleteDocument(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Documents where Id = @Id", new { Id = id });
    }

    public async Task DeleteAllDocuments()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Documents");
    }

    // ---------- Embeddings ----------

    internal async Task<int> WriteEmbedding(Chunk chunk, float[] vector, int documentId)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<int>(
            """
            insert into Embeddings (DocumentId, Text, Vector, TokenCount)
            output inserted.Id
            values (@DocumentId, @Text, @Vector, @TokenCount)
            """,
            new { DocumentId = documentId, Text = chunk.Text, Vector = FloatsToBytes(vector), TokenCount = chunk.Tokens.Length });
    }

    public async Task DeleteEmbeddingsForDocument(int documentId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Embeddings where DocumentId = @DocumentId", new { DocumentId = documentId });
    }

    public async Task<List<TextResult>> GetEmbeddedTextByIds(int[] ids)
    {
        if (ids.Length == 0) return [];

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<TextResult>(
            """
            select e.Id, e.Text, d.Filename
            from Embeddings e
            join Documents d on d.Id = e.DocumentId
            where e.Id in @Ids
            """,
            new { Ids = ids });
        return results.ToList();
    }

    internal async Task<List<VectorResult>> GetVectorsForCategory(string category)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            """
            select e.Id, e.Vector
            from Embeddings e
            join Documents d on d.Id = e.DocumentId
            where d.Category = @Category
            """,
            new { Category = category });

        return results.Select(r => new VectorResult
        {
            Id = r.Id,
            Vector = BytesToFloats((byte[])r.Vector)
        }).ToList();
    }

    public async Task DeleteAllEmbeddings()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Embeddings");
    }

    // ---------- Beats ----------

    public async Task<int> StoreBeat(string category, int sessionNumber, int? beatNumber, int? version, string content)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<int>(
            """
            insert into Beats (Category, SessionNumber, BeatNumber, Version, Content)
            output inserted.Id
            values (@Category, @SessionNumber, @BeatNumber, @Version, @Content)
            """,
            new { Category = category, SessionNumber = sessionNumber, BeatNumber = beatNumber, Version = version, Content = content });
    }

    public async Task<List<Beat>> GetBeatsForSession(string category, int sessionNumber)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<Beat>(
            """
            select Id, SessionNumber, BeatNumber, Version, Content
            from Beats
            where Category = @Category and SessionNumber = @SessionNumber
            order by Id
            """,
            new { Category = category, SessionNumber = sessionNumber });
        return results.ToList();
    }

    public async Task DeleteBeat(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Beats where Id = @Id", new { Id = id });
    }

    public async Task DeleteAllBeats()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Beats");
    }

    public async Task TestConnection()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.QueryAsync<dynamic>("select top 0 Id, Category, Filename, Content, Summary from Documents");
        await connection.QueryAsync<dynamic>("select top 0 Id, DocumentId, Text, Vector, TokenCount from Embeddings");
        await connection.QueryAsync<dynamic>("select top 0 Id, Category, SessionNumber, BeatNumber, Version, Content from Beats");
    }

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        if (bytes.Length % 4 != 0) return [];

        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
