using StarForged_Claude_MCP.Embeddings.Database.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace StarForged_Claude_MCP.Embeddings.Database;

public interface IDbInterface
{
    Task<int> WriteEmbedding(string text, float[] vector, string sourceDocument, int tokenCount);
    Task<TextResult?> GetText(int id);
    Task<List<TextResult>> GetTextByIds(int[] ids);
    Task DeleteEmbedding(int id);
    Task<List<VectorResult>> GetAllVectors();
    Task TestConnection();
}

public class DbInterface : IDbInterface
{
    private readonly string _connectionString;

    public DbInterface(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<int> WriteEmbedding(string text, float[] vector, string sourceDocument, int tokenCount)
    {
        using var connection = new SqlConnection(_connectionString);
        var id = await connection.QuerySingleAsync<int>(
            "INSERT INTO Embeddings (Text, Vector, SourceDocument, TokenCount) OUTPUT INSERTED.Id VALUES (@Text, @Vector, @SourceDocument, @TokenCount)",
            new { Text = text, Vector = FloatsToBytes(vector), SourceDocument = sourceDocument, TokenCount = tokenCount });
        return id;
    }

    public async Task<TextResult?> GetText(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT Id, Text, SourceDocument FROM Embeddings WHERE Id = @Id",
            new { Id = id });

        if (result == null) return null;

        return new TextResult
        {
            Id = result.Id,
            Text = result.Text,
            SourceDocument = result.SourceDocument
        };
    }

    public async Task<List<TextResult>> GetTextByIds(int[] ids)
    {
        if (ids.Length == 0) return [];

        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "SELECT Id, Text, SourceDocument FROM Embeddings WHERE Id IN @Ids",
            new { Ids = ids });

        return results.Select(r => new TextResult
        {
            Id = r.Id,
            Text = r.Text,
            SourceDocument = r.SourceDocument
        }).ToList();
    }

    public async Task DeleteEmbedding(int id)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM Embeddings WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<List<VectorResult>> GetAllVectors()
    {
        using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<dynamic>(
            "SELECT Id, Vector FROM Embeddings");

        return results.Select(r => new VectorResult
        {
            Id = r.Id,
            Vector = BytesToFloats((byte[])r.Vector)
        }).ToList();
    }

    public async Task TestConnection()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.QueryAsync<dynamic>("SELECT TOP 0 Id, Text, Vector, SourceDocument, TokenCount FROM Embeddings");
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
