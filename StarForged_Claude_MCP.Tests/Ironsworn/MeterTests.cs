using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Ironsworn;

public class MeterTests
{
    private static readonly StateTrackingId Health = new("health");

    [Fact]
    public void Create_WithAValueInRange_ShouldKeepTheValue()
    {
        var change = Created(min: 0, max: 5, value: 3);

        change.Meter.Name.Should().Be(Health);
        change.Meter.Value.Should().Be(3);
        change.Meter.Min.Should().Be(0);
        change.Meter.Max.Should().Be(5);
        change.Clamped.Should().Be(0);
    }

    [Theory]
    [InlineData(-2, 0, 2)]
    [InlineData(8, 5, 3)]
    public void Create_WithAValueOutOfRange_ShouldClampIt(int value, int expectedValue, int expectedClamped)
    {
        var change = Created(min: 0, max: 5, value: value);

        change.Meter.Value.Should().Be(expectedValue);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Fact]
    public void Create_WithMaxBelowMin_ShouldFailWithMeterRangeInvalid()
    {
        var result = Meter.Create(Health, min: 5, max: 4, value: 5);

        result.Should().BeOfType<FailureResult<MeterChange, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Meter_Range_Invalid);
    }

    [Fact]
    public void Create_WithMaxEqualToMin_ShouldHoldOnlyThatValue()
    {
        var change = Created(min: 2, max: 2, value: 7);

        change.Meter.Value.Should().Be(2);
        change.Clamped.Should().Be(5);
    }

    [Fact]
    public void Create_WithoutMax_ShouldBeUnboundedAbove()
    {
        var change = Created(min: 0, max: null, value: 1000);

        change.Meter.Max.Should().BeNull();
        change.Meter.Value.Should().Be(1000);
        change.Clamped.Should().Be(0);
    }

    [Theory]
    [InlineData(-2, 1, 0)]
    [InlineData(-5, 0, 2)]
    [InlineData(1, 4, 0)]
    [InlineData(4, 5, 2)]
    public void Adjust_ShouldStayWithinTheRange(int delta, int expectedValue, int expectedClamped)
    {
        var change = Created(min: 0, max: 5, value: 3).Meter.Adjust(delta);

        change.Meter.Value.Should().Be(expectedValue);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Fact]
    public void Adjust_WithoutMax_ShouldOnlyBeBoundedBelow()
    {
        var meter = Created(min: 0, max: null, value: 3).Meter;

        meter.Adjust(100).Meter.Value.Should().Be(103);
        meter.Adjust(-10).Should().BeEquivalentTo(new { Meter = new { Value = 0 }, Clamped = 7 });
    }

    [Theory]
    [InlineData(4, 4, 0)]
    [InlineData(-1, 0, 1)]
    [InlineData(9, 5, 4)]
    public void SetTo_ShouldStayWithinTheRange(int value, int expectedValue, int expectedClamped)
    {
        var change = Created(min: 0, max: 5, value: 3).Meter.SetTo(value);

        change.Meter.Value.Should().Be(expectedValue);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Fact]
    public void SetTo_WithoutMax_ShouldOnlyBeBoundedBelow()
    {
        var meter = Created(min: -3, max: null, value: 0).Meter;

        meter.SetTo(500).Meter.Value.Should().Be(500);
        meter.SetTo(-4).Should().BeEquivalentTo(new { Meter = new { Value = -3 }, Clamped = 1 });
    }

    [Fact]
    public void ChangingAMeter_ShouldLeaveTheOriginalAsItWas()
    {
        var meter = Created(min: 0, max: 5, value: 3).Meter;

        meter.Adjust(1);
        meter.SetTo(0);

        meter.Value.Should().Be(3);
    }

    private static MeterChange Created(int min, int? max, int value) =>
        Meter.Create(Health, min, max, value).Should().BeOfType<SuccessResult<MeterChange, ErrorCode>>().Subject.Value;
}
