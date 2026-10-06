using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.ConsoleAccess.CreateCampaign;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Tests.Server.Integration;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class CampaignCreatorTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();

    [Fact]
    public async Task Create_WithANewName_ShouldCreateTheCampaign()
    {
        var name = UniqueName();

        var (output, error) = await CreateCapturingOutput(name);

        var id = await Campaigns.GetCampaignByName(name);
        id.Should().NotBeNull();
        output.Should().Contain($"Created campaign '{name}' (id {id})");
        error.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheCampaignAlreadyExists_ShouldRefuseAndKeepTheExistingCampaign()
    {
        var name = UniqueName();
        var existingId = await Campaigns.CreateCampaign(name);

        var (output, error) = await CreateCapturingOutput(name);

        error.Should().Contain($"A campaign named '{name}' already exists.");
        output.Should().BeEmpty();
        (await Campaigns.GetCampaignByName(name)).Should().Be(existingId);
    }

    [Fact]
    public async Task Create_WithANameDifferingOnlyInCase_ShouldRefuse()
    {
        var name = UniqueName();
        var existingId = await Campaigns.CreateCampaign(name);
        var differentCase = name.ToUpperInvariant();

        var (output, error) = await CreateCapturingOutput(differentCase);

        error.Should().Contain($"A campaign named '{differentCase}' already exists.");
        output.Should().BeEmpty();
        (await Campaigns.GetCampaignByName(name)).Should().Be(existingId);
    }

    private static string UniqueName() => $"test-{Guid.NewGuid():N}"[..30];

    private async Task<(string Output, string Error)> CreateCapturingOutput(string name)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            await new CampaignCreator(Campaigns).Create(new CreateCampaignOptions(name));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (output.ToString(), error.ToString());
    }
}
