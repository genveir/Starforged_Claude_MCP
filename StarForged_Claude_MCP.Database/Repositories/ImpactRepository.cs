using Dapper;
using StarForged_Claude_MCP.Database.Models;

namespace StarForged_Claude_MCP.Database.Repositories;

/// <summary>
/// Impacts are keyed by campaign, impacted entity and name. Entities and names are compared under the columns'
/// case-insensitive collation, so an impact is found whatever casing it is asked for in, and keeps the casing it
/// was stored with.
/// </summary>
public class ImpactRepository
{
    private readonly DbConnectionFactory _connections;

    public ImpactRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<List<ImpactRow>> GetImpacts(int campaignId)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<ImpactRow>(
            "select ImpactedEntity, Name from Impacts where CampaignId = @CampaignId order by ImpactedEntity, Name",
            new { CampaignId = campaignId });
        return results.ToList();
    }

    public async Task<List<ImpactRow>> GetImpacts(int campaignId, string impactedEntity)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<ImpactRow>(
            """
            select ImpactedEntity, Name from Impacts
            where CampaignId = @CampaignId and ImpactedEntity = @ImpactedEntity
            order by Name
            """,
            new { CampaignId = campaignId, ImpactedEntity = impactedEntity });
        return results.ToList();
    }

    /// <summary>Inserts the impact unless the campaign already has it, in whatever casing.</summary>
    public async Task InsertImpact(int campaignId, string impactedEntity, string name)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            """
            insert into Impacts (CampaignId, ImpactedEntity, Name)
            select @CampaignId, @ImpactedEntity, @Name
            where not exists (
                select 1 from Impacts
                where CampaignId = @CampaignId and ImpactedEntity = @ImpactedEntity and Name = @Name)
            """,
            new { CampaignId = campaignId, ImpactedEntity = impactedEntity, Name = name });
    }

    public async Task DeleteImpact(int campaignId, string impactedEntity, string name)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            "delete from Impacts where CampaignId = @CampaignId and ImpactedEntity = @ImpactedEntity and Name = @Name",
            new { CampaignId = campaignId, ImpactedEntity = impactedEntity, Name = name });
    }
}
