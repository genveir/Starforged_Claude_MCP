using BERTTokenizers;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace StarForged_Claude_MCP.Embeddings.Services;

public sealed class EmbeddingsService : IDisposable
{
    private readonly VectorCacheService _vectorCache;
    private readonly InferenceSession _session;
    private readonly BertUncasedLargeTokenizer _tokenizer;

    public EmbeddingsService(VectorCacheService vectorCache, BertUncasedLargeTokenizer tokenizer, IConfiguration configuration)
    {
        _vectorCache = vectorCache;
        _tokenizer = tokenizer;

        var modelPath = Path.Combine(AppContext.BaseDirectory, "ExternalDependencies", "model.onnx");

        _session = new InferenceSession(modelPath);
    }

    public async Task<float[]> GenerateEmbeddings(string text)
    {
        await Task.CompletedTask;

        var tokens = _tokenizer.Encode(512, text);

        var inputIds = new DenseTensor<long>(new[] { 1, tokens.Count });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Count });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, tokens.Count });

        for (int i = 0; i < tokens.Count; i++)
        {
            inputIds[0, i] = tokens[i].InputIds;
            attentionMask[0, i] = tokens[i].AttentionMask;
            tokenTypeIds[0, i] = tokens[i].TokenTypeIds;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
        };

        using var results = _session.Run(inputs);
        using var firstResult = results.First();
        var outputTensor = firstResult.AsTensor<float>();

        var embedding = new float[outputTensor.Dimensions[1]];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = outputTensor[0, i];
        }

        return embedding;
    }

    public int CountTokens(string text)
    {
        var tokens = _tokenizer.Encode(512, text);
        return tokens.Count;
    }

    public async Task<int[]> PerformSimilaritySearch(string text, int topK)
    {
        var queryVector = await GenerateEmbeddings(text);

        var vectors = await _vectorCache.GetAllVectors();

        var similarities = vectors
            .Select(kvp => new { Id = kvp.Key, Similarity = CosineSimilarity(queryVector, kvp.Value) })
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Id)
            .ToArray();

        return similarities;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        float magnitude = MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB);
        return magnitude == 0 ? 0 : dotProduct / magnitude;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
