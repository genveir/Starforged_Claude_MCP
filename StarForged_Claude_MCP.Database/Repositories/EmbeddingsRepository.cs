using Dapper;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Util;

namespace StarForged_Claude_MCP.Database.Repositories;

public class EmbeddingsRepository
{
    private readonly DbConnectionFactory _connections;

    public EmbeddingsRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<int> WriteEmbedding(string text, int tokenCount, float[] vector, int documentId)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleAsync<int>(
            """
            insert into Embeddings (DocumentId, Text, Vector, TokenCount)
            output inserted.Id
            values (@DocumentId, @Text, @Vector, @TokenCount)
            """,
            new { DocumentId = documentId, Text = text, Vector = FloatsToBytes(vector), TokenCount = tokenCount });
    }

    public async Task DeleteEmbeddingsForDocument(int documentId)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync("delete from Embeddings where DocumentId = @DocumentId", new { DocumentId = documentId });
    }

    public async Task<List<TextResult>> GetEmbeddedTextByIds(int[] ids)
    {
        if (ids.Length == 0) return [];

        using var connection = _connections.Create();
        var results = await connection.QueryAsync<TextResult>(
            """
            select e.Id, e.Text, d.Category, d.Filename
            from Embeddings e
            join Documents d on d.Id = e.DocumentId
            where e.Id in @Ids
            """,
            new { Ids = ids });
        return results.ToList();
    }

    /// <summary>
    /// The vectors of every indexed document in <paramref name="category"/> or any category under it.
    /// </summary>
    public async Task<List<VectorResult>> GetVectorsForCategory(string category)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<dynamic>(
            $"""
            select e.Id, e.Vector
            from Embeddings e
            join Documents d on d.Id = e.DocumentId
            where {CategoryPath.InScopeSql}
            """,
            CategoryPath.InScopeParameters(category));

        return results.Select(r => new VectorResult
        {
            Id = r.Id,
            Vector = BytesToFloats((byte[])r.Vector)
        }).ToList();
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
