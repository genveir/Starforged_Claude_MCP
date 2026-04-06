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

    public async Task StoreDocument(string content, string sourceDocument, string? beatNumber = null)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "insert into Documents (Content, SourceDocument, BeatNumber) values (@Content, @SourceDocument, @BeatNumber)",
            new { Content = content, SourceDocument = sourceDocument, BeatNumber = beatNumber });
    }

    public async Task<List<DocumentResult>> GetAllDocumentsForSourceDocument(string sourceDocument)
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "select Content, SourceDocument, BeatNumber from Documents where SourceDocument = @SourceDocument order by Id",
            new { SourceDocument = sourceDocument });

        int sequence = 1;
        return results.Select(r => new DocumentResult
        {
            Content = r.Content,
            Sequence = sequence++,
            BeatNumber = (string?)r.BeatNumber
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
