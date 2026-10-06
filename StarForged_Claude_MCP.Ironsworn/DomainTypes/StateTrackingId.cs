using System.Text.RegularExpressions;

namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

public partial class StateTrackingId
{
    public string Value { get; }

    public StateTrackingId(string stateTrackingId)
    {
        if (!IsWellFormed(stateTrackingId))
            throw new ArgumentException(
                $"State tracking ID '{stateTrackingId}' is not well-formed: it can be up to 100 characters long, " +
                "and can contain only letters, digits, hyphens and at most one dot.");
        Value = stateTrackingId;
    }

    public static bool IsWellFormed(string stateTrackingId) =>
        stateTrackingId.Length <= 100 && _stateTrackingIdPattern.IsMatch(stateTrackingId);


    private static readonly Regex _stateTrackingIdPattern = StateTrackingIdPattern();

    [GeneratedRegex("^[a-zA-Z0-9-]+(\\.[a-zA-Z0-9-]+)?$", RegexOptions.Compiled)]
    private static partial Regex StateTrackingIdPattern();
}
