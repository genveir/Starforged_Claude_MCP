using Microsoft.Extensions.DependencyInjection;

namespace StarForged_Claude_MCP.Database;

public static class DatabaseServicesExtension
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
    {
        services.AddSingleton<DbInterface>();

        return services;
    }
}
