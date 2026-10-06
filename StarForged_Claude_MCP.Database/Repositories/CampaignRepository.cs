using Dapper;

namespace StarForged_Claude_MCP.Database.Repositories;

public class CampaignRepository
{
    private readonly DbConnectionFactory _connections;

    public CampaignRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<int?> GetCampaignByName(string campaignName)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "select Id from Campaigns where Name = @Name",
            new { Name = campaignName });
    }

    public async Task<int> CreateCampaign(string campaignName)
    {
        using var connection = _connections.Create();

        return await connection.QuerySingleAsync<int>(
            """
            insert into Campaigns (Name)
            output inserted.Id
            values (@Name)
            """,
            new { Name = campaignName });
    }
}
