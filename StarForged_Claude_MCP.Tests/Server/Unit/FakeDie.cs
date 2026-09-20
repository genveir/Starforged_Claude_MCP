using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

/// <summary>
/// A die that rolls the values it was given, in order, so a roll can be set up exactly.
/// </summary>
internal sealed class FakeDie : IDie
{
    private readonly Queue<int> _rolls;

    public FakeDie(params int[] rolls) => _rolls = new Queue<int>(rolls);

    public int Roll() => _rolls.Count > 0
        ? _rolls.Dequeue()
        : throw new InvalidOperationException("The die was rolled more often than the test set it up for");
}
