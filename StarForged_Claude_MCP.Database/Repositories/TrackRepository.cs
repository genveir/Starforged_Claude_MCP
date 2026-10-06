using Dapper;
using StarForged_Claude_MCP.Database.Models;

namespace StarForged_Claude_MCP.Database.Repositories;

/// <summary>
/// Tracks are keyed by campaign, kind and name. Kinds and names are compared under the columns' case-insensitive
/// collation, so a lookup finds a track whatever casing it is asked for in, and returns the casing it was stored with.
/// </summary>
public class TrackRepository
{
    private const string Columns = "Kind, Name, Rank, Description, Ticks";

    private readonly DbConnectionFactory _connections;

    public TrackRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<List<TrackRow>> GetTracks(int campaignId)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<TrackRow>(
            $"select {Columns} from Tracks where CampaignId = @CampaignId order by Kind, Name",
            new { CampaignId = campaignId });
        return results.ToList();
    }

    public async Task<List<TrackRow>> GetTracks(int campaignId, string kind)
    {
        using var connection = _connections.Create();
        var results = await connection.QueryAsync<TrackRow>(
            $"select {Columns} from Tracks where CampaignId = @CampaignId and Kind = @Kind order by Name",
            new { CampaignId = campaignId, Kind = kind });
        return results.ToList();
    }

    public async Task<TrackRow?> GetTrack(int campaignId, string kind, string name)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<TrackRow>(
            $"select {Columns} from Tracks where CampaignId = @CampaignId and Kind = @Kind and Name = @Name",
            new { CampaignId = campaignId, Kind = kind, Name = name });
    }

    public async Task InsertTrack(int campaignId, string kind, string name, string rank, string description, int ticks)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            """
            insert into Tracks (CampaignId, Kind, Name, Rank, Description, Ticks)
            values (@CampaignId, @Kind, @Name, @Rank, @Description, @Ticks)
            """,
            new { CampaignId = campaignId, Kind = kind, Name = name, Rank = rank, Description = description, Ticks = ticks });
    }

    public async Task UpdateTrackTicks(int campaignId, string kind, string name, int ticks)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            "update Tracks set Ticks = @Ticks where CampaignId = @CampaignId and Kind = @Kind and Name = @Name",
            new { CampaignId = campaignId, Kind = kind, Name = name, Ticks = ticks });
    }

    public async Task UpdateTrackDetails(int campaignId, string kind, string name, string description, string rank)
    {
        using var connection = _connections.Create();
        await connection.ExecuteAsync(
            """
            update Tracks set Description = @Description, Rank = @Rank
            where CampaignId = @CampaignId and Kind = @Kind and Name = @Name
            """,
            new { CampaignId = campaignId, Kind = kind, Name = name, Description = description, Rank = rank });
    }

    /// <summary>Deletes the track and returns the name it was stored under, or null when there was none to delete.</summary>
    public async Task<string?> DeleteTrack(int campaignId, string kind, string name)
    {
        using var connection = _connections.Create();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "delete from Tracks output deleted.Name where CampaignId = @CampaignId and Kind = @Kind and Name = @Name",
            new { CampaignId = campaignId, Kind = kind, Name = name });
    }
}
