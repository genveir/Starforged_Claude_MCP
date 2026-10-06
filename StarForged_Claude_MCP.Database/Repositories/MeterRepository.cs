using Dapper;
using StarForged_Claude_MCP.Database.Models;

namespace StarForged_Claude_MCP.Database.Repositories;

/// <summary>
/// Meters are keyed by campaign and name. Names are compared under the column's case-insensitive collation,
/// so a lookup finds a meter whatever casing it is asked for in, and returns the casing it was stored with.
/// </summary>
public class MeterRepository
{
    private readonly DbConnectionFactory _connections;

    public MeterRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<List<MeterRow>> GetMeters(int campaignId)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<MeterRow>(
            "select Name, Value, MinValue, MaxValue from Meters where CampaignId = @CampaignId order by Name",
            new { CampaignId = campaignId });
        return results.ToList();
    }

    public async Task<MeterRow?> GetMeter(int campaignId, string name)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<MeterRow>(
            "select Name, Value, MinValue, MaxValue from Meters where CampaignId = @CampaignId and Name = @Name",
            new { CampaignId = campaignId, Name = name });
    }

    public async Task InsertMeter(int campaignId, string name, int value, int minValue, int? maxValue)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            """
            insert into Meters (CampaignId, Name, Value, MinValue, MaxValue)
            values (@CampaignId, @Name, @Value, @MinValue, @MaxValue)
            """,
            new { CampaignId = campaignId, Name = name, Value = value, MinValue = minValue, MaxValue = maxValue });
    }

    public async Task UpdateMeterValue(int campaignId, string name, int value)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            "update Meters set Value = @Value where CampaignId = @CampaignId and Name = @Name",
            new { CampaignId = campaignId, Name = name, Value = value });
    }

    /// <summary>Deletes the meter and returns the name it was stored under, or null when there was none to delete.</summary>
    public async Task<string?> DeleteMeter(int campaignId, string name)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "delete from Meters output deleted.Name where CampaignId = @CampaignId and Name = @Name",
            new { CampaignId = campaignId, Name = name });
    }
}
