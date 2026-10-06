using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.Dice;
using StarForged_Claude_MCP.Ironsworn.Services;

namespace StarForged_Claude_MCP.Ironsworn;

public static class IronswornServiceExtension
{
    public static IServiceCollection AddIronswornServices(this IServiceCollection services)
    {
        services.AddSingleton<IDiceRoller>(_ => new DiceRoller(
            actionDie: new Die(sides: 6),
            firstChallengeDie: new Die(sides: 10),
            secondChallengeDie: new Die(sides: 10)));

        services.AddSingleton<CampaignResolver>();
        services.AddSingleton<IMeterService, MeterService>();

        return services;
    }
}
