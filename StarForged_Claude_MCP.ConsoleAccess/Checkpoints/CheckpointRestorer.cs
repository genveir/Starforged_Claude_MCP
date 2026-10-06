using StarForged_Claude_MCP.ConsoleAccess.Download;
using StarForged_Claude_MCP.Ironsworn.Abstractions;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

public class CheckpointRestorer
{
    private readonly ICheckpointService checkpoints;
    private readonly IConfirmPrompt confirmPrompt;

    public CheckpointRestorer(ICheckpointService checkpoints, IConfirmPrompt confirmPrompt)
    {
        this.checkpoints = checkpoints;
        this.confirmPrompt = confirmPrompt;
    }

    public async Task Restore(RestoreCheckpointOptions options)
    {
        var campaign = options.Campaign.Value;

        if (!options.Yes && !confirmPrompt.Confirm(
                $"Replace all meters, tracks and impacts of campaign '{campaign}' with checkpoint '{options.Name.Value}'?"))
        {
            Console.WriteLine("Cancelled. Nothing was restored.");
            return;
        }

        var restored = await checkpoints.RestoreCheckpoint(options.Campaign, options.Name);

        restored.Act(
            onSuccess: name => Console.WriteLine($"Restored checkpoint '{name.Value}' of campaign '{campaign}'."),
            onFailure: code => Console.Error.WriteLine(CheckpointErrors.Describe(code, options.Campaign, options.Name)));
    }
}
