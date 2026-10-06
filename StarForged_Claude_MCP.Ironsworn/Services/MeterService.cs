using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class MeterService : IMeterService
{
    private readonly CampaignResolver _campaignResolver;
    private readonly MeterRepository _meterRepository;

    public MeterService(CampaignResolver campaignResolver, MeterRepository meterRepository)
    {
        _campaignResolver = campaignResolver;
        _meterRepository = meterRepository;
    }

    public Task<Result<IReadOnlyList<Meter>, ErrorCode>> GetMeters(CampaignName campaign) =>
        InCampaign(campaign, operation: async campaignId =>
        {
            var rows = await _meterRepository.GetMeters(campaignId);

            return Result<IReadOnlyList<Meter>, ErrorCode>.Succeed(rows.Select(ToMeter).ToList());
        });

    public Task<Result<Meter, ErrorCode>> GetMeter(CampaignName campaign, StateTrackingId name) =>
        InCampaign(campaign, operation: async campaignId =>
        {
            var row = await _meterRepository.GetMeter(campaignId, name.Value);

            return row is null
                ? Result<Meter, ErrorCode>.Fail(ErrorCode.Meter_Not_Found)
                : Result<Meter, ErrorCode>.Succeed(ToMeter(row));
        });

    public Task<Result<MeterChange, ErrorCode>> CreateMeter(
        CampaignName campaign, StateTrackingId name, int min, int? max, int value) =>
        InCampaign(campaign, operation: campaignId => Meter.Create(name, min, max, value).Map(
            onSuccess: async created =>
            {
                if (await _meterRepository.GetMeter(campaignId, name.Value) is not null)
                    return Result<MeterChange, ErrorCode>.Fail(ErrorCode.Meter_Already_Exists);

                var meter = created.Meter;
                await _meterRepository.InsertMeter(campaignId, meter.Name.Value, meter.Value, meter.Min, meter.Max);

                return Result<MeterChange, ErrorCode>.Succeed(created);
            },
            onFailure: Result<MeterChange, ErrorCode>.FailAsTask));

    public Task<Result<MeterChange, ErrorCode>> AdjustMeter(CampaignName campaign, StateTrackingId name, int delta) =>
        ChangeMeter(campaign, name, change: meter => meter.Adjust(delta));

    public Task<Result<MeterChange, ErrorCode>> SetMeter(CampaignName campaign, StateTrackingId name, int value) =>
        ChangeMeter(campaign, name, change: meter => meter.SetTo(value));

    public Task<Result<StateTrackingId, ErrorCode>> RemoveMeter(CampaignName campaign, StateTrackingId name) =>
        InCampaign(campaign, operation: async campaignId =>
        {
            var removed = await _meterRepository.DeleteMeter(campaignId, name.Value);

            return removed is null
                ? Result<StateTrackingId, ErrorCode>.Fail(ErrorCode.Meter_Not_Found)
                : Result<StateTrackingId, ErrorCode>.Succeed(new StateTrackingId(removed));
        });

    private Task<Result<MeterChange, ErrorCode>> ChangeMeter(
        CampaignName campaign, StateTrackingId name, Func<Meter, MeterChange> change) =>
        InCampaign(campaign, operation: async campaignId =>
        {
            var row = await _meterRepository.GetMeter(campaignId, name.Value);
            if (row is null)
                return Result<MeterChange, ErrorCode>.Fail(ErrorCode.Meter_Not_Found);

            var changed = change(ToMeter(row));
            await _meterRepository.UpdateMeterValue(campaignId, row.Name, changed.Meter.Value);

            return Result<MeterChange, ErrorCode>.Succeed(changed);
        });

    private async Task<Result<T, ErrorCode>> InCampaign<T>(
        CampaignName campaign, Func<int, Task<Result<T, ErrorCode>>> operation)
    {
        var campaignId = await _campaignResolver.ResolveCampaignIdByName(campaign);

        return await campaignId.Map(
            onSuccess: operation,
            onFailure: Result<T, ErrorCode>.FailAsTask);
    }

    private static Meter ToMeter(MeterRow row) =>
        new(new StateTrackingId(row.Name), row.Value, row.MinValue, row.MaxValue);
}
