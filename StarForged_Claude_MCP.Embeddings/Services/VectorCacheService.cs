using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.Embeddings.Services;

internal class VectorCacheService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Dictionary<int, float[]> _vectorCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public VectorCacheService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshCache();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task RefreshCache()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbInterface = scope.ServiceProvider.GetRequiredService<DbInterface>();

        var vectors = await dbInterface.GetAllVectors();

        await _cacheLock.WaitAsync();
        try
        {
            _vectorCache = vectors.ToDictionary(v => v.Id, v => v.Vector);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<Dictionary<int, float[]>> GetAllVectors()
    {
        await _cacheLock.WaitAsync();
        try
        {
            return new Dictionary<int, float[]>(_vectorCache);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<int?> FindExistingVector(float[] vector)
    {
        await _cacheLock.WaitAsync();
        try
        {
            foreach (var kvp in _vectorCache)
            {
                if (VectorsAreEqual(kvp.Value, vector))
                {
                    return kvp.Key;
                }
            }
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static bool VectorsAreEqual(float[] a, float[] b) => a.SequenceEqual(b);
}
