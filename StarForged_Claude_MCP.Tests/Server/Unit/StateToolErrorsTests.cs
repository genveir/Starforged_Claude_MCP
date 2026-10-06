using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Server.Tools;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class StateToolErrorsTests
{
    private readonly ToolArguments _arguments = new(new Dictionary<string, object> { ["campaign"] = "Iron Expanse" });

    [Fact]
    public void ToArgumentException_ForCampaignNotFound_ShouldNameTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Campaign_Not_Found, _arguments);

        exception.Message.Should().Contain("'Iron Expanse'");
    }

    [Fact]
    public void ToArgumentException_ForNone_ShouldThrowInvalidOperationException()
    {
        var act = () => StateToolErrors.ToArgumentException(ErrorCode.None, _arguments);

        act.Should().Throw<InvalidOperationException>();
    }
}
