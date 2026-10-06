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
        ["track"] = "vow.handle-the-plantation",
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
    public void ToArgumentException_ForTrackIdNeedsKind_ShouldNameTheTrackAndListTheKinds()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Track_Id_Needs_Kind, WithTrack("handle-the-plantation"));

        exception.Message.Should().Contain("'handle-the-plantation'").And.Contain("'vow.handle-the-plantation'")
            .And.Contain("vow, connection, expedition and combat");
    }

    [Fact]
    public void ToArgumentException_ForTrackKindUnknown_ShouldNameThePrefixAndListTheKinds()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Track_Kind_Unknown, WithTrack("quest.handle-the-plantation"));

        exception.Message.Should().StartWith("'quest' is not a track kind").And.Contain("vow, connection, expedition and combat");
    }

    [Fact]
    public void ToArgumentException_ForTrackNotFound_ShouldNameTheTrackAndTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Track_Not_Found, _arguments);

        exception.Message.Should().Contain("'vow.handle-the-plantation'").And.Contain("'Iron Expanse'").And.Contain("get_tracks");
    }

    [Fact]
    public void ToArgumentException_ForTrackAlreadyExists_ShouldNameTheTrackAndTheCampaign()
    {
        var exception = StateToolErrors.ToArgumentException(ErrorCode.Track_Already_Exists, _arguments);

        exception.Message.Should().Contain("'vow.handle-the-plantation'").And.Contain("'Iron Expanse'")
            .And.Contain("edit_track").And.Contain("update_track").And.Contain("remove_track");
    }

    [Fact]
    public void ToArgumentException_ForNone_ShouldThrowInvalidOperationException()
    {
        var act = () => StateToolErrors.ToArgumentException(ErrorCode.None, _arguments);

        act.Should().Throw<InvalidOperationException>();
    }

    private static ToolArguments WithTrack(string track) =>
        new(new Dictionary<string, object> { ["campaign"] = "Iron Expanse", ["track"] = track });
}
