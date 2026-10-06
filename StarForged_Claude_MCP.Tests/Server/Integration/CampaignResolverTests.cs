using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Services;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

[Collection("McpServer")]
public class CampaignResolverTests
{
    private readonly TestFixture _fixture;
    private readonly CampaignRepository _campaigns;
    private readonly CampaignResolver _resolver;

    public CampaignResolverTests(TestFixture fixture)
    {
        _fixture = fixture;
        _campaigns = fixture.Services.GetRequiredService<CampaignRepository>();
        _resolver = fixture.Services.GetRequiredService<CampaignResolver>();
    }

    [Fact]
    public async Task ResolveCampaignIdByName_WhenTheCampaignExists_ShouldReturnItsId()
    {
        await _fixture.ClearCampaigns();
        var id = await _campaigns.CreateCampaign("Iron Expanse");

        var resolved = await _resolver.ResolveCampaignIdByName(new CampaignName("Iron Expanse"));

        resolved.Should().Be(id);
    }

    [Fact]
    public async Task ResolveCampaignIdByName_WhenTheCampaignDoesNotExist_ShouldReturnNull()
    {
        await _fixture.ClearCampaigns();
        await _campaigns.CreateCampaign("Iron Expanse");

        var resolved = await _resolver.ResolveCampaignIdByName(new CampaignName("Forge Drift"));

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveCampaignIdByName_WithSeveralCampaigns_ShouldReturnTheIdOfTheNamedOne()
    {
        await _fixture.ClearCampaigns();
        await _campaigns.CreateCampaign("Iron Expanse");
        var id = await _campaigns.CreateCampaign("Forge Drift");
        await _campaigns.CreateCampaign("Void Reach");

        var resolved = await _resolver.ResolveCampaignIdByName(new CampaignName("Forge Drift"));

        resolved.Should().Be(id);
    }

    [Fact]
    public async Task ResolveCampaignIdByName_WithANameDifferingOnlyInCase_ShouldReturnTheExistingCampaign()
    {
        await _fixture.ClearCampaigns();
        var id = await _campaigns.CreateCampaign("Iron Expanse");

        var resolved = await _resolver.ResolveCampaignIdByName(new CampaignName("IRON EXPANSE"));

        resolved.Should().Be(id);
    }
}
