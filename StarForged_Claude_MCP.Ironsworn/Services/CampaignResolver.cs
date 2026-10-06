using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class CampaignResolver
{
    private readonly CampaignRepository _campaignRepository;

    public CampaignResolver(CampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<Result<int, ErrorCode>> ResolveCampaignIdByName(CampaignName campaignName)
    {
        var campaignId = await _campaignRepository.GetCampaignByName(campaignName.Value);

        return campaignId is { } id
            ? Result<int, ErrorCode>.Succeed(id)
            : Result<int, ErrorCode>.Fail(ErrorCode.Campaign_Not_Found);
    }
}
