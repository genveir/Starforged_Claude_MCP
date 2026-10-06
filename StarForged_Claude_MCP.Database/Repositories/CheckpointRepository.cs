using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using StarForged_Claude_MCP.Database.Models;

namespace StarForged_Claude_MCP.Database.Repositories;

/// <summary>
/// Checkpoints are keyed by campaign and name, compared under the column's case-insensitive collation. A checkpoint
/// holds a JSON snapshot of the campaign's meters, tracks and impacts. Saving and restoring each run in one
/// transaction, so a restore that fails partway leaves the campaign as it was.
/// </summary>
public class CheckpointRepository
{
    private readonly DbConnectionFactory _connections;

    public CheckpointRepository(DbConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>Snapshots the campaign's meters, tracks and impacts, overwriting any checkpoint of that name.</summary>
    public async Task SaveCheckpoint(int campaignId, string name)
    {
        using var connection = _connections.Create();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var snapshot = await ReadSnapshot(connection, transaction, campaignId);

        await connection.ExecuteAsync(
            """
            update Checkpoints set Snapshot = @Snapshot where CampaignId = @CampaignId and Name = @Name;
            if @@rowcount = 0
                insert into Checkpoints (CampaignId, Name, Snapshot) values (@CampaignId, @Name, @Snapshot);
            """,
            new { CampaignId = campaignId, Name = name, Snapshot = JsonSerializer.Serialize(snapshot) },
            transaction);

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Replaces all of the campaign's meters, tracks and impacts with the checkpoint's, keeping the checkpoint.
    /// Returns false, having changed nothing, when the campaign has no checkpoint of that name.
    /// </summary>
    public async Task<bool> RestoreCheckpoint(int campaignId, string name)
    {
        using var connection = _connections.Create();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var json = await connection.QuerySingleOrDefaultAsync<string?>(
            "select Snapshot from Checkpoints where CampaignId = @CampaignId and Name = @Name",
            new { CampaignId = campaignId, Name = name },
            transaction);
        if (json is null)
            return false;

        var snapshot = JsonSerializer.Deserialize<Snapshot>(json)!;

        await connection.ExecuteAsync(
            """
            delete from Meters where CampaignId = @CampaignId;
            delete from Tracks where CampaignId = @CampaignId;
            delete from Impacts where CampaignId = @CampaignId;
            """,
            new { CampaignId = campaignId },
            transaction);

        await connection.ExecuteAsync(
            """
            insert into Meters (CampaignId, Name, Value, MinValue, MaxValue)
            values (@CampaignId, @Name, @Value, @MinValue, @MaxValue)
            """,
            snapshot.Meters.Select(meter => new { CampaignId = campaignId, meter.Name, meter.Value, meter.MinValue, meter.MaxValue }),
            transaction);
        await connection.ExecuteAsync(
            """
            insert into Tracks (CampaignId, Kind, Name, Rank, Description, Ticks)
            values (@CampaignId, @Kind, @Name, @Rank, @Description, @Ticks)
            """,
            snapshot.Tracks.Select(track => new { CampaignId = campaignId, track.Kind, track.Name, track.Rank, track.Description, track.Ticks }),
            transaction);
        await connection.ExecuteAsync(
            "insert into Impacts (CampaignId, ImpactedEntity, Name) values (@CampaignId, @ImpactedEntity, @Name)",
            snapshot.Impacts.Select(impact => new { CampaignId = campaignId, impact.ImpactedEntity, impact.Name }),
            transaction);

        await transaction.CommitAsync();
        return true;
    }

    private static async Task<Snapshot> ReadSnapshot(SqlConnection connection, DbTransaction transaction, int campaignId)
    {
        using var reader = await connection.QueryMultipleAsync(
            """
            select Name, Value, MinValue, MaxValue from Meters where CampaignId = @CampaignId;
            select Kind, Name, Rank, Description, Ticks from Tracks where CampaignId = @CampaignId;
            select ImpactedEntity, Name from Impacts where CampaignId = @CampaignId;
            """,
            new { CampaignId = campaignId },
            transaction);

        return new Snapshot(
            Meters: (await reader.ReadAsync<MeterRow>()).ToList(),
            Tracks: (await reader.ReadAsync<TrackRow>()).ToList(),
            Impacts: (await reader.ReadAsync<ImpactRow>()).ToList());
    }

    private record Snapshot(List<MeterRow> Meters, List<TrackRow> Tracks, List<ImpactRow> Impacts);
}
