using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

[Collection("McpServer")]
public class CampaignRepositoryTests
{
    private readonly CampaignRepository _campaigns;

    public CampaignRepositoryTests(TestFixture fixture)
    {
        _campaigns = fixture.Services.GetRequiredService<CampaignRepository>();
    }

    [Fact]
    public async Task CreateCampaign_ShouldStoreTheCampaignAndReturnItsId()
    {
        var name = UniqueName();

        var id = await _campaigns.CreateCampaign(name);

        id.Should().BePositive();
        (await _campaigns.GetCampaignByName(name)).Should().Be(id);
    }

    [Fact]
    public async Task GetCampaignByName_WhenTheCampaignDoesNotExist_ShouldReturnNull()
    {
        var name = $"missing-{Guid.NewGuid():N}";

        (await _campaigns.GetCampaignByName(name)).Should().BeNull();
    }

    [Fact]
    public async Task GetCampaignByName_WithANameDifferingOnlyInCase_ShouldReturnTheExistingCampaign()
    {
        var name = UniqueName();
        var id = await _campaigns.CreateCampaign(name);

        (await _campaigns.GetCampaignByName(name.ToUpperInvariant())).Should().Be(id);
    }

    [Fact]
    public async Task CreateCampaign_WithANameDifferingOnlyInCase_ShouldBeRejected()
    {
        var name = UniqueName();
        await _campaigns.CreateCampaign(name);

        var create = () => _campaigns.CreateCampaign(name.ToUpperInvariant());

        await create.Should().ThrowAsync<SqlException>(because: "campaign names are unique regardless of case");
    }

    private static string UniqueName() => $"test-{Guid.NewGuid():N}"[..30];
}
