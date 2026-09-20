namespace StarForged_Claude_MCP.Server.Services;

public class Die : IDie
{
    private readonly int _sides;

    public Die(int sides)
    {
        if (sides < 1)
            throw new ArgumentOutOfRangeException(nameof(sides), sides, "A die needs at least one side");

        _sides = sides;
    }

    public int Roll() => Random.Shared.Next(1, _sides + 1);
}
