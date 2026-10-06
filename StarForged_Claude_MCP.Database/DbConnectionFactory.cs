using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace StarForged_Claude_MCP.Database;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public SqlConnection Create() => new(_connectionString);

    public async Task TestConnection()
    {
        using var connection = Create();
        await connection.QueryAsync<dynamic>("select top 0 Id, Category, Filename, Content, Summary from Documents");
        await connection.QueryAsync<dynamic>("select top 0 Id, DocumentId, Text, Vector, TokenCount from Embeddings");

        await connection.QueryAsync<dynamic>("select top 0 Id, Name from Campaigns");
        await connection.QueryAsync<dynamic>("select top 0 CampaignId, Name, Value, MinValue, MaxValue from Meters");
        await connection.QueryAsync<dynamic>("select top 0 CampaignId, Kind, Name, Rank, Description, Ticks from Tracks");
        await connection.QueryAsync<dynamic>("select top 0 CampaignId, ImpactedEntity, Name from Impacts");
        await connection.QueryAsync<dynamic>("select top 0 CampaignId, Name, Snapshot from Checkpoints");
    }
}
