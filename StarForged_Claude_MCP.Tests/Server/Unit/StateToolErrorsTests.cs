using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Server.Tools;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class StateToolErrorsTests
{
    private readonly ToolArguments _arguments = new(new Dictionary<string, object>
    {
        ["campaign"] = "Iron Expanse",
        ["name"] = "jorran-hasfer.integrity",
        ["min"] = 3,
        ["max"] = 1
    });

    [Fact]
    public void ToArgumentException_ForCampaignNotFound_ShouldNameTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Campaign_Not_Found, _arguments);

        exception.Message.Should().Contain("'Iron Expanse'");
    }

    [Fact]
    public void ToArgumentException_ForMeterNotFound_ShouldNameTheMeterAndTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Meter_Not_Found, _arguments);

        exception.Message.Should().Contain("'jorran-hasfer.integrity'").And.Contain("'Iron Expanse'").And.Contain("get_meters");
    }

    [Fact]
    public void ToArgumentException_ForMeterAlreadyExists_ShouldNameTheMeterAndTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Meter_Already_Exists, _arguments);

        exception.Message.Should().Contain("'jorran-hasfer.integrity'").And.Contain("'Iron Expanse'")
            .And.Contain("update_meter").And.Contain("remove_meter");
    }

    [Fact]
    public void ToArgumentException_ForMeterRangeInvalid_ShouldQuoteTheMaxAndTheMin()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Meter_Range_Invalid, _arguments);

        exception.Message.Should().Contain("Max 1").And.Contain("min 3");
    }

    [Fact]
    public void ToArgumentException_ForNone_ShouldThrowInvalidOperationException()
    {
        var act = () => StateToolErrors.ToArgumentException(ErrorCode.None, _arguments);

        act.Should().Throw<InvalidOperationException>();
    }
}
