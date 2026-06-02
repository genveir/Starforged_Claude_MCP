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

    internal async Task<int> WriteEmbedding(Chunk chunk, float[] vector, string sourceDocument)
    {
        using var connection = new SqlConnection(_connectionString);
        var id = await connection.QuerySingleAsync<int>(
            "insert into Embeddings (Text, Vector, SourceDocument, TokenCount) output inserted.Id values (@Text, @Vector, @SourceDocument, @TokenCount)",
            new { Text = chunk.Text, Vector = FloatsToBytes(vector), SourceDocument = sourceDocument, TokenCount = chunk.Tokens.Length });
        return id;
    }

    public async Task<List<TextResult>> GetEmbeddedTextByIds(int[] ids)
    {
        if (ids.Length == 0) return [];

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "select Id, Text, SourceDocument from Embeddings where Id in @Ids",
            new { Ids = ids });

        return results.Select(r => new TextResult
        {
            Id = r.Id,
            Text = r.Text,
            SourceDocument = r.SourceDocument
        }).ToList();
    }

    public async Task DeleteAllEmbeddings()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Embeddings");
    }

    public async Task DeleteAllDocuments()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("delete from Documents");
    }

    public async Task StoreDocument(string content, string sourceDocument, string? beatNumber = null, string? summary = null, string? category = null)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "insert into Documents (Content, SourceDocument, BeatNumber, Summary, Category) values (@Content, @SourceDocument, @BeatNumber, @Summary, @Category)",
            new { Content = content, SourceDocument = sourceDocument, BeatNumber = beatNumber, Summary = summary, Category = category });
    }

    public async Task<List<DocumentResult>> GetAllDocumentsForSourceDocument(string sourceDocument, string? category = null)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "select Content, BeatNumber, Summary, Category from Documents where SourceDocument = @SourceDocument and ((@Category is null and Category is null) or Category = @Category) order by Id",
            new { SourceDocument = sourceDocument, Category = category });

        int sequence = 1;
        return results.Select(r => new DocumentResult
        {
            Content = r.Content,
            Sequence = sequence++,
            BeatNumber = (string?)r.BeatNumber,
            Summary = (string?)r.Summary,
            Category = (string?)r.Category
        }).ToList();
    }

    public async Task<List<DocumentIndexEntry>> GetDistinctSourceDocuments(string? category = null)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            """
            with DistinctSummaries as (
                select distinct SourceDocument, Summary
                from Documents
                where Summary is not null
                and ((@Category is null and Category is null) or Category = @Category)
            )
            select d.SourceDocument, string_agg(ds.Summary, ', ') as Summaries
            from (select distinct SourceDocument from Documents where ((@Category is null and Category is null) or Category = @Category)) d
            left join DistinctSummaries ds on ds.SourceDocument = d.SourceDocument
            group by d.SourceDocument
            order by d.SourceDocument
            """,
            new { Category = category });
        return results.Select(r => new DocumentIndexEntry
        {
            SourceDocument = r.SourceDocument,
            Summaries = (string?)r.Summaries
        }).ToList();
    }

    public async Task<List<string?>> GetBeats(string sourceDocument)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<string?>(
            "select BeatNumber from Documents where SourceDocument = @SourceDocument order by Id",
            new { SourceDocument = sourceDocument });
        return results.ToList();
    }

    internal async Task<List<VectorResult>> GetAllVectors()
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "select Id, Vector from Embeddings");

        return results.Select(r => new VectorResult
        {
            Id = r.Id,
            Vector = BytesToFloats((byte[])r.Vector)
        }).ToList();
    }

    public async Task TestConnection()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.QueryAsync<dynamic>("select top 0 Id, Text, Vector, SourceDocument, TokenCount from Embeddings");
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
