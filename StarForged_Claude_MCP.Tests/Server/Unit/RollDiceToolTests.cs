using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class RollDiceToolTests
{
    [Fact]
    public async Task RollDice_ShouldReturnTheResolvedRollUnderTheNamesTheModelReads()
    {
        var payload = await CallRollDiceAsync(
            arguments: new Dictionary<string, object> { ["add"] = 3 },
            actionDie: 4,
            firstChallengeDie: 2,
            secondChallengeDie: 10);

        payload.GetProperty("actionDie").GetInt32().Should().Be(4);
        payload.GetProperty("add").GetInt32().Should().Be(3);
        payload.GetProperty("actionScore").GetInt32().Should().Be(7);
        payload.GetProperty("challengeDice").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(2, 10);
        payload.GetProperty("resultType").GetString().Should().Be("weak hit");
        payload.GetProperty("match").GetBoolean().Should().BeFalse();
        payload.GetProperty("d100").GetString().Should().Be("20");
    }

    [Fact]
    public async Task RollDice_WithoutAnAdd_ShouldTreatTheAddAsZero()
    {
        var payload = await CallRollDiceAsync(
            arguments: new Dictionary<string, object>(),
            actionDie: 4,
            firstChallengeDie: 2,
            secondChallengeDie: 10);

        payload.GetProperty("add").GetInt32().Should().Be(0);
        payload.GetProperty("actionScore").GetInt32().Should().Be(4);
    }

    private static async Task<JsonElement> CallRollDiceAsync(
        Dictionary<string, object> arguments, int actionDie, int firstChallengeDie, int secondChallengeDie)
    {
        var roller = new DiceRoller(
            actionDie: new FakeDie(actionDie),
            firstChallengeDie: new FakeDie(firstChallengeDie),
            secondChallengeDie: new FakeDie(secondChallengeDie));

        var server = new McpServer(
            new Mock<IEmbeddingsFacade>(MockBehavior.Strict).Object,
            new Mock<IDocumentsFacade>(MockBehavior.Strict).Object,
            roller,
            new WritePermissions(),
            NullLogger<McpServer>.Instance);

        var response = await McpServerInvoker.HandleRequestAsync(server, new JsonRpcRequest
        {
            Id = "roll",
            Method = "tools/call",
            Params = new CallToolParams { Name = "roll_dice", Arguments = arguments }
        });

        response.Error.Should().BeNull();
        var text = ((CallToolResult)response.Result!).Content.Single().Text;

        return JsonSerializer.Deserialize<JsonElement>(text);
    }
}
