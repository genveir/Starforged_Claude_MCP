using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;

namespace StarForged_Claude_MCP.Database;

public static class DatabaseServicesExtension
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
    {
        services.AddSingleton<DbConnectionFactory>();

        services.AddSingleton<DocumentsRepository>();
        services.AddSingleton<EmbeddingsRepository>();
        services.AddSingleton<CampaignRepository>();
        services.AddSingleton<MeterRepository>();

        return services;
    }
}
