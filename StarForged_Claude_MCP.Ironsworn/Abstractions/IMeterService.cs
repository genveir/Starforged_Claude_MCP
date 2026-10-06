using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Abstractions;

/// <summary>
/// The meters of a campaign. Every operation fails with <see cref="ErrorCode.Campaign_Not_Found"/> when the
/// campaign does not exist, and every operation but creation with <see cref="ErrorCode.Meter_Not_Found"/>
/// when the meter does not.
/// </summary>
public interface IMeterService
{
    /// <summary>All of the campaign's meters, ordered by name.</summary>
    Task<Result<IReadOnlyList<Meter>, ErrorCode>> GetMeters(CampaignName campaign);

    Task<Result<Meter, ErrorCode>> GetMeter(CampaignName campaign, StateTrackingId name);

    /// <summary>
    /// Fails with <see cref="ErrorCode.Meter_Range_Invalid"/> when max is below min, and with
    /// <see cref="ErrorCode.Meter_Already_Exists"/> when the campaign already has a meter of that name.
    /// </summary>
    Task<Result<MeterChange, ErrorCode>> CreateMeter(CampaignName campaign, StateTrackingId name, int min, int? max, int value);

    Task<Result<MeterChange, ErrorCode>> AdjustMeter(CampaignName campaign, StateTrackingId name, int delta);

    Task<Result<MeterChange, ErrorCode>> SetMeter(CampaignName campaign, StateTrackingId name, int value);

    /// <summary>Removes the meter and returns the name it was stored under.</summary>
    Task<Result<StateTrackingId, ErrorCode>> RemoveMeter(CampaignName campaign, StateTrackingId name);
}
