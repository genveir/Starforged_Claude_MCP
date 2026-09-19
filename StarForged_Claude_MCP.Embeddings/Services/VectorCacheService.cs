using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.Embeddings.Services;

internal class VectorCacheService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Vectors by id, held in a separate cache per category. Every read is scoped to one
    /// category: there is no search across the whole database, and dedup only ever matches
    /// within the category being written to. Categories compare case-insensitively, as they
    /// do in SQL.
    /// </summary>
    private Dictionary<string, Dictionary<int, float[]>> _vectorCache = NewCache();

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
            var cache = NewCache();
            foreach (var vector in vectors)
            {
                if (!cache.TryGetValue(vector.Category, out var categoryCache))
                {
                    categoryCache = [];
                    cache[vector.Category] = categoryCache;
                }
                categoryCache[vector.Id] = vector.Vector;
            }
            _vectorCache = cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<Dictionary<int, float[]>> GetAllVectors(string category)
    {
        await _cacheLock.WaitAsync();
        try
        {
            return _vectorCache.TryGetValue(category, out var categoryCache)
                ? new Dictionary<int, float[]>(categoryCache)
                : [];
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task AddVector(int id, float[] vector, string category)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (!_vectorCache.TryGetValue(category, out var categoryCache))
            {
                categoryCache = [];
                _vectorCache[category] = categoryCache;
            }
            categoryCache[id] = vector;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<int?> FindExistingVector(float[] vector, string category)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (!_vectorCache.TryGetValue(category, out var categoryCache)) return null;

            foreach (var kvp in categoryCache)
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

    private static Dictionary<string, Dictionary<int, float[]>> NewCache() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static bool VectorsAreEqual(float[] a, float[] b) => a.SequenceEqual(b);
}
