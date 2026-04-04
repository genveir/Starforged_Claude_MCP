using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Embeddings.Services;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Embeddings
{
    public static class EmbeddingsServicesExtension
    {
        public static IServiceCollection AddEmbeddingsServices(this IServiceCollection services)
        {
            services.AddSingleton<MarkdownPreprocessor>();
            services.AddSingleton<UnchunkableFlatTextPreprocessor>();
            services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<EmbeddingsService>();
            services.AddSingleton<DbInterface>();
            services.AddSingleton<VectorCacheService>();

            services.AddHostedService(sp => sp.GetRequiredService<VectorCacheService>());

            return services;
        }
    }
}
