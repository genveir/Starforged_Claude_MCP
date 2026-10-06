using StarForged_Claude_MCP.Ironsworn.Abstractions;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

public class CheckpointCreator
{
    private readonly ICheckpointService checkpoints;

    public CheckpointCreator(ICheckpointService checkpoints)
    {
        this.checkpoints = checkpoints;
    }

    public async Task Create(CreateCheckpointOptions options)
    {
        var created = await checkpoints.CreateCheckpoint(options.Campaign, options.Name);

        created.Act(
            onSuccess: name => Console.WriteLine($"Saved checkpoint '{name.Value}' of campaign '{options.Campaign.Value}'."),
            onFailure: code => Console.Error.WriteLine(CheckpointErrors.Describe(code, options.Campaign, options.Name)));
    }
}
