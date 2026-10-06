using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

/// <summary>
/// A named value held within a range. The meter knows nothing of what it measures; it only keeps its value
/// between <see cref="Min"/> and <see cref="Max"/>, where a null max leaves it unbounded above.
/// </summary>
public class Meter
{
    public StateTrackingId Name { get; }
    public int Value { get; }
    public int Min { get; }
    public int? Max { get; }

    /// <summary>Rebuilds a meter as stored, without validating it: only <see cref="Create"/> lets new ranges in.</summary>
    internal Meter(StateTrackingId name, int value, int min, int? max)
    {
        Name = name;
        Value = value;
        Min = min;
        Max = max;
    }

    public static Result<MeterChange, ErrorCode> Create(StateTrackingId name, int min, int? max, int value)
    {
        if (max < min)
            return Result<MeterChange, ErrorCode>.Fail(ErrorCode.Meter_Range_Invalid);

        return Result<MeterChange, ErrorCode>.Succeed(new Meter(name, min, min, max).SetTo(value));
    }

    public MeterChange Adjust(int delta) => ClampTo(Value + delta);

    public MeterChange SetTo(int value) => ClampTo(value);

    private MeterChange ClampTo(int target)
    {
        var value = Math.Clamp(target, Min, Max ?? int.MaxValue);

        return new MeterChange(new Meter(Name, value, Min, Max), Clamped: Math.Abs(target - value));
    }
}

/// <summary>The meter after a change, and how much of the change did not fit its range: 0 when all of it did.</summary>
public record MeterChange(Meter Meter, int Clamped);
